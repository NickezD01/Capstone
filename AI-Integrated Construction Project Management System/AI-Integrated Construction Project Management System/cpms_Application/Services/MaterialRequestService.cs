using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Response;
using cpms_Application.Response.MaterialRequest;
using cpms_Domain.Models;
using cpms_Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace cpms_Application.Services
{
    public class MaterialRequestService : IMaterialRequestService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public MaterialRequestService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateRequestAsync(CreateMaterialRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.PM)) return Forbidden("Only project managers may create material requests.");
            if (request.Items == null || request.Items.Count == 0)
                return new ApiResponse().SetBadRequest(message: "At least one material item is required.");
            if (!request.TaskId.HasValue)
                return new ApiResponse().SetBadRequest(message: "TaskId is required so every request remains capped by an approved task material plan.");

            var project = await _uow.Projects.GetByIdAsync(request.ProjectId);
            if (project == null) return new ApiResponse().SetNotFound(message: "Project not found.");
            if (project.PMUserID != user.Id) return Forbidden("You may only create requests for a project you manage.");
            if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                return new ApiResponse().SetConflict(message: "Paused or closed projects cannot accept new material requests.");
            if (request.TaskId.HasValue)
            {
                var task = await _uow.TaskItems.GetByIdAsync(request.TaskId.Value);
                if (task == null || task.ProjectId != request.ProjectId)
                    return new ApiResponse().SetBadRequest(message: "Task does not belong to the selected project.");
                if (task.Status is cpms_Domain.Models.TaskStatus.COMPLETED or cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED)
                    return new ApiResponse().SetConflict(message: "Closed tasks cannot accept material requests.");
            }
            if (request.WarehouseId.HasValue && await _uow.Warehouses.GetByIdAsync(request.WarehouseId.Value) == null)
                return new ApiResponse().SetBadRequest(message: "Assigned warehouse does not exist.");

            var resolved = new List<(MaterialItemRequest Item, MaterialVariant Variant)>();
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0) return new ApiResponse().SetBadRequest(message: "Requested quantity must be greater than zero.");
                var variant = await ResolveVariantAsync(item.VariantId, item.MaterialId);
                if (variant == null || !variant.IsActive)
                    return new ApiResponse().SetBadRequest(message: "One or more material variants do not exist or are inactive.");
                resolved.Add((item, variant));
            }
            if (resolved.GroupBy(x => x.Variant.VariantId).Any(g => g.Count() > 1))
                return new ApiResponse().SetBadRequest(message: "A material variant may only appear once per request.");

            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var currentProject = await _uow.Projects.GetByIdAsync(request.ProjectId);
                if (currentProject == null)
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetNotFound(message: "Project not found.");
                }
                if (currentProject.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetConflict(message: "Paused or closed projects cannot accept new material requests.");
                }
                if (request.TaskId.HasValue)
                {
                    var existingActiveRequest = await _uow.MaterialRequests.GetAsync(r =>
                        r.TaskId == request.TaskId.Value &&
                        (r.Status == MaterialRequestStatuses.Pending ||
                         r.Status == MaterialRequestStatuses.Approved ||
                         r.Status == MaterialRequestStatuses.PartiallyApproved ||
                         r.Status == MaterialRequestStatuses.PartiallyIssued));
                    if (existingActiveRequest != null)
                    {
                        await _uow.RollbackTransactionAsync();
                        return new ApiResponse().SetConflict(message: "This task already has a pending or approved material request.");
                    }

                    var plannedRequirements = await _uow.TaskMaterialRequirements.GetAllAsync(r => r.TaskId == request.TaskId.Value);
                    var issuedItems = await _uow.MaterialRequisitions.GetAllAsync(r =>
                        r.MaterialRequest.TaskId == request.TaskId.Value && r.IssuedQuantity > 0,
                        q => q.Include(r => r.MaterialRequest));
                    var returnedItems = await _uow.MaterialReturns.GetAllAsync(r =>
                        r.MaterialRequest.TaskId == request.TaskId.Value,
                        q => q.Include(r => r.MaterialRequest));
                    var issuedByVariant = issuedItems.GroupBy(r => r.VariantId)
                        .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));
                    var returnedByVariant = returnedItems.GroupBy(r => r.VariantId)
                        .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

                    foreach (var entry in resolved)
                    {
                        var planned = plannedRequirements.SingleOrDefault(r => r.VariantId == entry.Variant.VariantId);
                        if (planned == null)
                        {
                            await _uow.RollbackTransactionAsync();
                            return new ApiResponse().SetBadRequest(message: $"Variant {entry.Variant.VariantId} is not planned for this task.");
                        }
                        var netIssued = Math.Max(0,
                            issuedByVariant.GetValueOrDefault(entry.Variant.VariantId) - returnedByVariant.GetValueOrDefault(entry.Variant.VariantId));
                        var remaining = Math.Max(0, planned.GrossQuantityRequired - netIssued);
                        if (entry.Item.Quantity > remaining)
                        {
                            await _uow.RollbackTransactionAsync();
                            return new ApiResponse().SetConflict(message: $"Requested quantity for variant {entry.Variant.VariantId} exceeds the remaining task requirement of {remaining}.");
                        }
                    }
                }
                var entity = new MaterialRequest
                {
                    ProjectId = request.ProjectId,
                    Project = currentProject,
                    TaskId = request.TaskId,
                    WarehouseId = request.WarehouseId,
                    RequestedBy = user.Id,
                    RequestDate = DateTime.UtcNow,
                    Status = MaterialRequestStatuses.Pending,
                    RequestNote = request.RequestNote
                };
                await _uow.MaterialRequests.AddAsync(entity);
                await _uow.SaveChangeAsync();

                foreach (var entry in resolved)
                {
                    var requisition = new MaterialRequisition
                    {
                        RequestId = entity.RequestId,
                        MaterialRequest = entity,
                        VariantId = entry.Variant.VariantId,
                        Variant = entry.Variant,
                        Quantity = entry.Item.Quantity,
                        NeededByDate = entry.Item.NeededByDate,
                        Note = entry.Item.Note
                    };
                    entity.Requisitions.Add(requisition);
                    await _uow.MaterialRequisitions.AddAsync(requisition);
                }
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetRequestByIdAsync(entity.RequestId);
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to create material request.");
            }
        }

        public async Task<ApiResponse> CreateRequestByTaskIdAsync(int taskId)
        {
            var task = await _uow.TaskItems.GetAsync(t => t.TaskId == taskId,
                q => q.Include(t => t.MaterialRequirements));
            if (task == null) return new ApiResponse().SetNotFound(message: "Task not found.");
            if (task.MaterialRequirements.Count == 0)
                return new ApiResponse().SetBadRequest(message: "Task has no planned material requirements.");

            var issuedItems = await _uow.MaterialRequisitions.GetAllAsync(r =>
                r.MaterialRequest.TaskId == taskId && r.IssuedQuantity > 0,
                q => q.Include(r => r.MaterialRequest));
            var returnedItems = await _uow.MaterialReturns.GetAllAsync(r =>
                r.MaterialRequest.TaskId == taskId,
                q => q.Include(r => r.MaterialRequest));
            var issuedByVariant = issuedItems.GroupBy(r => r.VariantId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));
            var returnedByVariant = returnedItems.GroupBy(r => r.VariantId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
            var remainingItems = task.MaterialRequirements
                .Select(r => new
                {
                    Requirement = r,
                    Remaining = Math.Max(0, r.GrossQuantityRequired - Math.Max(0,
                        issuedByVariant.GetValueOrDefault(r.VariantId) - returnedByVariant.GetValueOrDefault(r.VariantId)))
                })
                .Where(x => x.Remaining > 0)
                .ToList();
            if (remainingItems.Count == 0)
                return new ApiResponse().SetConflict(message: "All planned material quantities for this task have already been issued.");

            return await CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = task.ProjectId,
                TaskId = taskId,
                Items = remainingItems.Select(x => new MaterialItemRequest
                {
                    VariantId = x.Requirement.VariantId,
                    Quantity = x.Remaining,
                    NeededByDate = task.BaselineStart
                }).ToList()
            });
        }

        public Task<ApiResponse> ApproveRequestAsync(int requestId) =>
            Task.FromResult(new ApiResponse().SetBadRequest(message: "WarehouseId and approved item quantities are required."));

        public async Task<ApiResponse> ApproveRequestAsync(int requestId, ApproveMaterialRequest decision)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return Forbidden("Only warehouse managers may approve material requests.");
            if (decision == null || decision.WarehouseId <= 0 || decision.Items.Count == 0)
                return new ApiResponse().SetBadRequest(message: "WarehouseId and approved item quantities are required.");
            if (decision.Items.All(x => x.ApprovedQuantity == 0))
                return new ApiResponse().SetBadRequest(message: "At least one request item must have a positive approved quantity; otherwise reject the request.");
            var warehouse = await _uow.Warehouses.GetByIdAsync(decision.WarehouseId);
            if (warehouse == null)
                return new ApiResponse().SetBadRequest(message: "Warehouse not found.");
            if (warehouse.ManagerId != user.Id)
                return Forbidden("You may only approve requests against a warehouse you manage.");

            await _uow.BeginTransactionAsync();
            try
            {
                var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId,
                    q => q.Include(r => r.Requisitions));
                if (request == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Material request not found."); }
                if (request.WarehouseId.HasValue && request.WarehouseId.Value != decision.WarehouseId)
                { await _uow.RollbackTransactionAsync(); return Forbidden("This request is assigned to another warehouse."); }
                var project = await _uow.Projects.GetByIdAsync(request.ProjectId);
                if (project == null)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Project not found."); }
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Material requests cannot be approved while the project is paused or closed."); }
                var task = request.TaskId.HasValue ? await _uow.TaskItems.GetByIdAsync(request.TaskId.Value) : null;
                if (task?.Status is cpms_Domain.Models.TaskStatus.COMPLETED or cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Material requests cannot be approved for a closed task."); }
                if (request.Status != MaterialRequestStatuses.Pending)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only pending requests can be approved."); }
                if (decision.Items.Select(i => i.ItemId).Distinct().Count() != decision.Items.Count ||
                    decision.Items.Any(i => request.Requisitions.All(r => r.ItemId != i.ItemId)))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Approval contains invalid or duplicate request items."); }

                foreach (var item in request.Requisitions)
                {
                    var approved = decision.Items.FirstOrDefault(i => i.ItemId == item.ItemId)?.ApprovedQuantity ?? 0;
                    if (approved < 0 || approved > item.Quantity)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Approved quantity must be between zero and requested quantity."); }
                    item.ApprovedQuantity = approved;
                    if (approved == 0) continue;

                    var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == decision.WarehouseId && i.VariantId == item.VariantId);
                    if (inventory == null || !InventoryQuantityRules.CanReserve(inventory.QuantityOnHand, inventory.ReservedQuantity, inventory.QuarantineQuantity, approved))
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: $"Insufficient available inventory for request item {item.ItemId}."); }
                    inventory.ReservedQuantity += approved;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    await _uow.InventoryReservations.AddAsync(new InventoryReservation
                    {
                        InventoryId = inventory.InventoryId,
                        RequestId = request.RequestId,
                        RequestItemId = item.ItemId,
                        Quantity = approved,
                        Status = InventoryReservationStatuses.Active,
                        ReservedAt = DateTime.UtcNow,
                        CreatedBy = user.Id
                    });
                }

                request.WarehouseId = decision.WarehouseId;
                request.Status = request.Requisitions.Any(i => i.ApprovedQuantity < i.Quantity)
                    ? MaterialRequestStatuses.PartiallyApproved
                    : MaterialRequestStatuses.Approved;
                request.ApprovedByUserId = user.Id;
                request.ApprovedAt = DateTime.UtcNow;
                request.DecisionNote = decision.DecisionNote;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetRequestByIdAsync(requestId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while the request was being approved. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to approve material request.");
            }
        }

        public Task<ApiResponse> RejectRequestAsync(int requestId) => RejectRequestAsync(requestId, new RejectMaterialRequest());

        public async Task<ApiResponse> RejectRequestAsync(int requestId, RejectMaterialRequest decision)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return Forbidden("Only warehouse managers may reject material requests.");
            var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId,
                q => q.Include(r => r.Warehouse!));
            if (request == null) return new ApiResponse().SetNotFound(message: "Material request not found.");
            if (request.WarehouseId.HasValue && request.Warehouse?.ManagerId != user.Id)
                return Forbidden("You may only reject requests assigned to a warehouse you manage.");
            if (request.Status != MaterialRequestStatuses.Pending)
                return new ApiResponse().SetConflict(message: "Only pending requests can be rejected.");
            request.Status = MaterialRequestStatuses.Rejected;
            request.ApprovedByUserId = user.Id;
            request.ApprovedAt = DateTime.UtcNow;
            request.DecisionNote = decision?.DecisionNote;
            await _uow.SaveChangeAsync();
            return await GetRequestByIdAsync(requestId);
        }

        public async Task<ApiResponse> IssueRequestAsync(int requestId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return Forbidden("Only warehouse managers may issue inventory.");
            await _uow.BeginTransactionAsync();
            try
            {
                var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId,
                    q => q.Include(r => r.Project).Include(r => r.Warehouse).Include(r => r.Requisitions).Include(r => r.Reservations).ThenInclude(r => r.InventoryRecord));
                if (request == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Material request not found."); }
                if (request.Warehouse == null || request.Warehouse.ManagerId != user.Id)
                { await _uow.RollbackTransactionAsync(); return Forbidden("You may only issue from a warehouse you manage."); }
                if (request.Project.Status is ProjectStatus.CANCELLED or ProjectStatus.COMPLETED or ProjectStatus.PAUSED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Inventory cannot be issued while the project is paused or closed."); }
                if (request.TaskId.HasValue)
                {
                    var task = await _uow.TaskItems.GetByIdAsync(request.TaskId.Value);
                    if (task?.Status is cpms_Domain.Models.TaskStatus.COMPLETED or cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Inventory cannot be issued to a closed task."); }
                }
                if (request.Status != MaterialRequestStatuses.Approved && request.Status != MaterialRequestStatuses.PartiallyApproved)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only approved requests can be issued."); }
                var active = request.Reservations.Where(r => r.Status == InventoryReservationStatuses.Active).ToList();
                if (active.Count == 0) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "No active reservations exist for this request."); }

                foreach (var reservation in active)
                {
                    var inventory = reservation.InventoryRecord;
                    if (!InventoryQuantityRules.CanIssue(inventory.QuantityOnHand, inventory.ReservedQuantity, inventory.QuarantineQuantity, reservation.Quantity))
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Reserved inventory is no longer available."); }
                    var before = inventory.QuantityOnHand;
                    inventory.QuantityOnHand -= reservation.Quantity;
                    inventory.ReservedQuantity -= reservation.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    reservation.Status = InventoryReservationStatuses.Fulfilled;
                    reservation.FulfilledAt = DateTime.UtcNow;
                    var item = request.Requisitions.Single(i => i.ItemId == reservation.RequestItemId);
                    item.IssuedQuantity += reservation.Quantity;
                    await _uow.InventoryTransactions.AddAsync(new InventoryTransaction
                    {
                        InventoryId = inventory.InventoryId,
                        VariantId = inventory.VariantId,
                        WarehouseId = inventory.WarehouseId,
                        TransactionType = InventoryTransactionTypes.Issue,
                        Quantity = -reservation.Quantity,
                        QuantityBefore = before,
                        QuantityAfter = inventory.QuantityOnHand,
                        ReferenceId = requestId,
                        ReferenceType = "MATERIAL_REQUEST",
                        UnitCost = inventory.AverageUnitCost,
                        TotalValue = reservation.Quantity * inventory.AverageUnitCost,
                        PerformedByUserId = user.Id,
                        TransactionDate = DateTime.UtcNow
                    });
                }
                request.Status = request.Requisitions.Any(i => i.IssuedQuantity < i.Quantity)
                    ? MaterialRequestStatuses.PartiallyIssued
                    : MaterialRequestStatuses.Issued;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetRequestByIdAsync(requestId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while issuing. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to issue inventory.");
            }
        }

        public async Task<ApiResponse> ReleaseRequestAsync(int requestId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return Forbidden("Only warehouse managers may release reservations.");
            await _uow.BeginTransactionAsync();
            try
            {
                var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId,
                    q => q.Include(r => r.Warehouse).Include(r => r.Reservations).ThenInclude(r => r.InventoryRecord));
                if (request == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Material request not found."); }
                if (request.Warehouse == null || request.Warehouse.ManagerId != user.Id)
                { await _uow.RollbackTransactionAsync(); return Forbidden("You may only release reservations from a warehouse you manage."); }
                if (request.Status != MaterialRequestStatuses.Approved &&
                    request.Status != MaterialRequestStatuses.PartiallyApproved &&
                    request.Status != MaterialRequestStatuses.PartiallyIssued)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only approved or partially issued requests can be released."); }
                foreach (var reservation in request.Reservations.Where(r => r.Status == InventoryReservationStatuses.Active))
                {
                    reservation.InventoryRecord.ReservedQuantity -= reservation.Quantity;
                    reservation.InventoryRecord.UpdatedAt = DateTime.UtcNow;
                    reservation.Status = InventoryReservationStatuses.Released;
                    reservation.ReleasedAt = DateTime.UtcNow;
                }
                request.Status = MaterialRequestStatuses.Released;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetRequestByIdAsync(requestId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while releasing reservations. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to release reservations.");
            }
        }

        public async Task<ApiResponse> UpdatePendingRequestAsync(int requestId, UpdatePendingMaterialRequest update)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.PM)) return Forbidden("Only project managers may edit material requests.");
            if (update.Items.Count == 0 || update.Items.Any(i => i.Quantity <= 0))
                return new ApiResponse().SetBadRequest(message: "Every request item must have a positive quantity.");

            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId,
                    q => q.Include(r => r.Project).Include(r => r.Requisitions));
                if (request == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Material request not found."); }
                if (request.Project.PMUserID != user.Id) { await _uow.RollbackTransactionAsync(); return Forbidden("You may only edit requests for a project you manage."); }
                if (request.Status != MaterialRequestStatuses.Pending) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only pending requests can be edited."); }
                if (!MatchesRowVersion(request.RowVersion, update.RowVersion)) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "The request was changed by another user. Reload and retry."); }
                if (update.Items.Select(i => i.ItemId).Distinct().Count() != request.Requisitions.Count ||
                    update.Items.Any(i => request.Requisitions.All(r => r.ItemId != i.ItemId)))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Editing must include each existing request item exactly once."); }

                var planned = await _uow.TaskMaterialRequirements.GetAllAsync(r => r.TaskId == request.TaskId);
                var previouslyIssued = await _uow.MaterialRequisitions.GetAllAsync(r =>
                    r.MaterialRequest.TaskId == request.TaskId && r.MaterialRequest.RequestId != requestId && r.IssuedQuantity > 0,
                    q => q.Include(r => r.MaterialRequest));
                var previouslyReturned = await _uow.MaterialReturns.GetAllAsync(r =>
                    r.MaterialRequest.TaskId == request.TaskId && r.MaterialRequest.RequestId != requestId,
                    q => q.Include(r => r.MaterialRequest));
                var issuedByVariant = previouslyIssued.GroupBy(r => r.VariantId).ToDictionary(g => g.Key, g => g.Sum(x => x.IssuedQuantity));
                var returnedByVariant = previouslyReturned.GroupBy(r => r.VariantId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                foreach (var item in request.Requisitions)
                {
                    var replacement = update.Items.Single(i => i.ItemId == item.ItemId);
                    var cap = planned.SingleOrDefault(p => p.VariantId == item.VariantId)?.GrossQuantityRequired ?? 0;
                    var netIssued = Math.Max(0,
                        issuedByVariant.GetValueOrDefault(item.VariantId) - returnedByVariant.GetValueOrDefault(item.VariantId));
                    var remaining = Math.Max(0, cap - netIssued);
                    if (replacement.Quantity > remaining) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: $"Quantity for variant {item.VariantId} exceeds the remaining task plan of {remaining}."); }
                    item.Quantity = replacement.Quantity;
                    item.NeededByDate = replacement.NeededByDate;
                    item.Note = replacement.Note;
                }
                request.RequestNote = update.RequestNote;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetRequestByIdAsync(requestId);
            }
            catch (DbUpdateConcurrencyException) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "The request changed while it was being edited. Reload and retry."); }
            catch { await _uow.RollbackTransactionAsync(); throw; }
        }

        public async Task<ApiResponse> CancelPendingRequestAsync(int requestId, CancelMaterialRequest cancellation)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.PM)) return Forbidden("Only project managers may cancel material requests.");
            var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId, q => q.Include(r => r.Project));
            if (request == null) return new ApiResponse().SetNotFound(message: "Material request not found.");
            if (request.Project.PMUserID != user.Id) return Forbidden("You may only cancel requests for a project you manage.");
            if (request.Status != MaterialRequestStatuses.Pending) return new ApiResponse().SetConflict(message: "Only pending requests can be cancelled by the project manager.");
            if (!MatchesRowVersion(request.RowVersion, cancellation.RowVersion)) return new ApiResponse().SetConflict(message: "The request was changed by another user. Reload and retry.");
            request.Status = MaterialRequestStatuses.Cancelled;
            request.DecisionNote = cancellation.Reason;
            await _uow.SaveChangeAsync();
            return await GetRequestByIdAsync(requestId);
        }

        public async Task<ApiResponse> GetRequestByIdAsync(int requestId)
        {
            var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId, RequestIncludes());
            if (request == null) return new ApiResponse().SetNotFound(message: "Material request not found.");
            if (!CanReadRequest(_claimService.GetUserClaim(), request))
                return Forbidden("You do not have access to this material request.");
            var response = await MapResponsesAsync(new[] { request });
            return new ApiResponse().SetOk(response.Single());
        }

        public async Task<ApiResponse> GetAllRequestsAsync()
        {
            var user = _claimService.GetUserClaim();
            System.Linq.Expressions.Expression<Func<MaterialRequest, bool>>? accessFilter = user.Role.ToUpperInvariant() switch
            {
                nameof(Role.ADMIN) => null,
                nameof(Role.PM) => r => r.Project.PMUserID == user.Id,
                nameof(Role.WAREHOUSE_MANAGER) => r =>
                    (!r.WarehouseId.HasValue && r.Status == MaterialRequestStatuses.Pending) ||
                    (r.WarehouseId.HasValue && r.Warehouse!.ManagerId == user.Id),
                _ => r => false
            };
            var requests = await _uow.MaterialRequests.GetAllAsync(accessFilter, RequestIncludes());
            return new ApiResponse().SetOk(await MapResponsesAsync(requests));
        }

        public async Task<ApiResponse> GetRequestsByProjectAsync(int projectId)
        {
            var user = _claimService.GetUserClaim();
            System.Linq.Expressions.Expression<Func<MaterialRequest, bool>> accessFilter = user.Role.ToUpperInvariant() switch
            {
                nameof(Role.ADMIN) => r => r.ProjectId == projectId,
                nameof(Role.PM) => r => r.ProjectId == projectId && r.Project.PMUserID == user.Id,
                nameof(Role.WAREHOUSE_MANAGER) => r => r.ProjectId == projectId &&
                    ((!r.WarehouseId.HasValue && r.Status == MaterialRequestStatuses.Pending) ||
                     (r.WarehouseId.HasValue && r.Warehouse!.ManagerId == user.Id)),
                _ => r => false
            };
            var requests = await _uow.MaterialRequests.GetAllAsync(accessFilter, RequestIncludes());
            return new ApiResponse().SetOk(await MapResponsesAsync(requests));
        }

        private static Func<IQueryable<MaterialRequest>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<MaterialRequest, object>> RequestIncludes() =>
            q => q.Include(r => r.Project)
                  .Include(r => r.Requester)
                  .Include(r => r.Warehouse)
                  .Include(r => r.Requisitions).ThenInclude(i => i.Variant).ThenInclude(v => v.Material);

        private async Task<List<MaterialRequestResponse>> MapResponsesAsync(IEnumerable<MaterialRequest> requests)
        {
            var requestList = requests.ToList();
            var responses = _mapper.Map<List<MaterialRequestResponse>>(requestList);
            if (requestList.Count == 0) return responses;

            var requestIds = requestList.Select(r => r.RequestId).ToList();
            var requestReturns = await _uow.MaterialReturns.GetAllAsync(r => requestIds.Contains(r.MaterialRequestId));
            var returnedByRequestVariant = requestReturns
                .GroupBy(r => (r.MaterialRequestId, r.VariantId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            var taskIds = requestList.Where(r => r.TaskId.HasValue).Select(r => r.TaskId!.Value).Distinct().ToList();
            var plannedByTaskVariant = new Dictionary<(int TaskId, int VariantId), decimal>();
            var issuedByTaskVariant = new Dictionary<(int TaskId, int VariantId), decimal>();
            var returnedByTaskVariant = new Dictionary<(int TaskId, int VariantId), decimal>();
            if (taskIds.Count > 0)
            {
                var requirements = await _uow.TaskMaterialRequirements.GetAllAsync(r => taskIds.Contains(r.TaskId));
                plannedByTaskVariant = requirements
                    .GroupBy(r => (r.TaskId, r.VariantId))
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.GrossQuantityRequired));

                var issued = await _uow.MaterialRequisitions.GetAllAsync(r =>
                    r.MaterialRequest.TaskId.HasValue && taskIds.Contains(r.MaterialRequest.TaskId.Value) && r.IssuedQuantity > 0,
                    q => q.Include(r => r.MaterialRequest));
                issuedByTaskVariant = issued
                    .GroupBy(r => (r.MaterialRequest.TaskId!.Value, r.VariantId))
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));

                var returned = await _uow.MaterialReturns.GetAllAsync(r =>
                    r.MaterialRequest.TaskId.HasValue && taskIds.Contains(r.MaterialRequest.TaskId.Value),
                    q => q.Include(r => r.MaterialRequest));
                returnedByTaskVariant = returned
                    .GroupBy(r => (r.MaterialRequest.TaskId!.Value, r.VariantId))
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
            }

            var sourceById = requestList.ToDictionary(r => r.RequestId);
            foreach (var response in responses)
            {
                var source = sourceById[response.RequestId];
                foreach (var item in response.Items)
                {
                    item.ReturnedQuantity = returnedByRequestVariant.GetValueOrDefault((response.RequestId, item.VariantId));
                    item.NetIssuedQuantity = Math.Max(0, item.IssuedQuantity - item.ReturnedQuantity);
                    item.RemainingRequestQuantity = Math.Max(0, item.Quantity - item.NetIssuedQuantity);
                    if (!source.TaskId.HasValue)
                    {
                        item.RemainingTaskDemand = item.RemainingRequestQuantity;
                        continue;
                    }

                    var key = (source.TaskId.Value, item.VariantId);
                    var taskNetIssued = Math.Max(0,
                        issuedByTaskVariant.GetValueOrDefault(key) - returnedByTaskVariant.GetValueOrDefault(key));
                    item.RemainingTaskDemand = Math.Max(0,
                        plannedByTaskVariant.GetValueOrDefault(key) - taskNetIssued);
                }
            }
            return responses;
        }

        private async Task<MaterialVariant?> ResolveVariantAsync(int variantId, int legacyMaterialId)
        {
            if (variantId != 0) return await _uow.MaterialVariants.GetByIdAsync(variantId);
            var candidates = await _uow.MaterialVariants.GetAllAsync(v => v.MaterialId == legacyMaterialId && v.IsActive);
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static bool IsRole(ClaimDTO claim, Role role) => string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool CanReadRequest(ClaimDTO claim, MaterialRequest request) =>
            IsRole(claim, Role.ADMIN) ||
            (IsRole(claim, Role.PM) && request.Project.PMUserID == claim.Id) ||
            (IsRole(claim, Role.WAREHOUSE_MANAGER) &&
             ((!request.WarehouseId.HasValue && request.Status == MaterialRequestStatuses.Pending) ||
              request.Warehouse?.ManagerId == claim.Id));
        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
        private static ApiResponse InternalError(string message) =>
            new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, message);
        private static bool MatchesRowVersion(byte[] current, string supplied)
        {
            if (current.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(supplied)) return false;
            try
            {
                return CryptographicOperations.FixedTimeEquals(current, Convert.FromBase64String(supplied));
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
