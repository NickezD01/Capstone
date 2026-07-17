using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Response;
using cpms_Application.Response.MaterialRequest;
using cpms_Domain.Models;
using cpms_Domain;
using Microsoft.EntityFrameworkCore;

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

            var project = await _uow.Projects.GetByIdAsync(request.ProjectId);
            if (project == null) return new ApiResponse().SetNotFound(message: "Project not found.");
            if (project.PMUserID != user.Id) return Forbidden("You may only create requests for a project you manage.");
            if (request.TaskId.HasValue)
            {
                var task = await _uow.TaskItems.GetByIdAsync(request.TaskId.Value);
                if (task == null || task.ProjectId != request.ProjectId)
                    return new ApiResponse().SetBadRequest(message: "Task does not belong to the selected project.");
            }

            var resolved = new List<(MaterialItemRequest Item, int VariantId)>();
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0) return new ApiResponse().SetBadRequest(message: "Requested quantity must be greater than zero.");
                var variant = await ResolveVariantAsync(item.VariantId, item.MaterialId);
                if (variant == null || !variant.IsActive)
                    return new ApiResponse().SetBadRequest(message: "One or more material variants do not exist or are inactive.");
                resolved.Add((item, variant.VariantId));
            }
            if (resolved.GroupBy(x => x.VariantId).Any(g => g.Count() > 1))
                return new ApiResponse().SetBadRequest(message: "A material variant may only appear once per request.");

            await _uow.BeginTransactionAsync();
            try
            {
                var entity = new MaterialRequest
                {
                    ProjectId = request.ProjectId,
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
                    await _uow.MaterialRequisitions.AddAsync(new MaterialRequisition
                    {
                        RequestId = entity.RequestId,
                        VariantId = entry.VariantId,
                        Quantity = entry.Item.Quantity,
                        NeededByDate = entry.Item.NeededByDate,
                        Note = entry.Item.Note
                    });
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

            return await CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = task.ProjectId,
                TaskId = taskId,
                Items = task.MaterialRequirements.Select(r => new MaterialItemRequest
                {
                    VariantId = r.VariantId,
                    Quantity = r.GrossQuantityRequired,
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
                    if (inventory == null || !InventoryQuantityRules.CanReserve(inventory.QuantityOnHand, inventory.ReservedQuantity, approved))
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
                request.Status = MaterialRequestStatuses.Approved;
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
                    q => q.Include(r => r.Warehouse).Include(r => r.Requisitions).Include(r => r.Reservations).ThenInclude(r => r.InventoryRecord));
                if (request == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Material request not found."); }
                if (request.Warehouse == null || request.Warehouse.ManagerId != user.Id)
                { await _uow.RollbackTransactionAsync(); return Forbidden("You may only issue from a warehouse you manage."); }
                if (request.Status != MaterialRequestStatuses.Approved)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only approved requests can be issued."); }
                var active = request.Reservations.Where(r => r.Status == InventoryReservationStatuses.Active).ToList();
                if (active.Count == 0) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "No active reservations exist for this request."); }

                foreach (var reservation in active)
                {
                    var inventory = reservation.InventoryRecord;
                    if (!InventoryQuantityRules.CanIssue(inventory.QuantityOnHand, inventory.ReservedQuantity, reservation.Quantity))
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
                        PerformedByUserId = user.Id,
                        TransactionDate = DateTime.UtcNow
                    });
                }
                request.Status = MaterialRequestStatuses.Issued;
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
                if (request.Status != MaterialRequestStatuses.Approved)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only approved requests can be released."); }
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

        public async Task<ApiResponse> GetRequestByIdAsync(int requestId)
        {
            var request = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestId, RequestIncludes());
            if (request == null) return new ApiResponse().SetNotFound(message: "Material request not found.");
            if (!CanReadRequest(_claimService.GetUserClaim(), request))
                return Forbidden("You do not have access to this material request.");
            return new ApiResponse().SetOk(_mapper.Map<MaterialRequestResponse>(request));
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
            return new ApiResponse().SetOk(_mapper.Map<List<MaterialRequestResponse>>(requests));
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
            return new ApiResponse().SetOk(_mapper.Map<List<MaterialRequestResponse>>(requests));
        }

        private static Func<IQueryable<MaterialRequest>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<MaterialRequest, object>> RequestIncludes() =>
            q => q.Include(r => r.Project)
                  .Include(r => r.Requester)
                  .Include(r => r.Warehouse)
                  .Include(r => r.Requisitions).ThenInclude(i => i.Variant).ThenInclude(v => v.Material);

        private async Task<MaterialVariant?> ResolveVariantAsync(int variantId, int legacyMaterialId) =>
            variantId != 0 ? await _uow.MaterialVariants.GetByIdAsync(variantId)
                : await _uow.MaterialVariants.GetAsync(v => v.MaterialId == legacyMaterialId && v.IsActive);

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
    }
}
