using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.Project;
using cpms_Application.Response;
using cpms_Application.Response.MaterialRequest;
using cpms_Application.Response.Project;
using cpms_Application.Response.Tasks;
using cpms_Domain.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WordXml = DocumentFormat.OpenXml.Wordprocessing;

namespace cpms_Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper, IClaimService claimService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateProjectAsync(CreateProjectRequest request)
        {
            var apiResponse = new ApiResponse();

            try
            {
                var currentUser = _claimService.GetUserClaim();
                if (!string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || request.PMUserID != currentUser.Id)
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "A project manager may only create a project assigned to their own account.");
                if (request.TotalProjectBudget < 0 || request.BaselineEnd < request.BaselineStart ||
                    request.StartDate < request.BaselineStart || request.StartDate > request.BaselineEnd)
                    return apiResponse.SetBadRequest("Project budget cannot be negative and the start date must be inside the baseline period.");
                // Kiểm tra PM có tồn tại không
                var pm = await _unitOfWork.UserAccounts.GetByIdAsync(request.PMUserID);

                if (pm == null)
                {
                    return apiResponse.SetNotFound("Project manager not found.");
                }

                // Mapping Request -> Entity
                var project = _mapper.Map<Project>(request);
                project.Status = ProjectStatus.PLANNING;

                // Lưu Project
                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangeAsync();

                // Lấy lại Project kèm Navigation Properties
                var createdProject = (await _unitOfWork.Projects.GetAllAsync(
                    filter: p => p.ProjectId == project.ProjectId,
                    include: query => query
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Tasks)
                        .Include(p => p.PurchaseOrders).ThenInclude(o => o.OrderLineItems)
                        .Include(p => p.AIAlerts)
                )).FirstOrDefault();

                if (createdProject == null)
                {
                    return apiResponse.SetNotFound("The newly created project could not be reloaded.");
                }

                // Mapping sang Response
                var response = _mapper.Map<ProjectResponse>(createdProject);

                return apiResponse.SetOk(response);
            }
            catch (Exception)
            {
                return InternalError("Unable to create the project.");
            }
        }

        public async Task<ApiResponse> GetAllProjectsAsync()
        {
            var apiResponse = new ApiResponse();

            try
            {
                var currentUser = _claimService.GetUserClaim();
                System.Linq.Expressions.Expression<Func<Project, bool>>? accessFilter = currentUser.Role.ToUpperInvariant() switch
                {
                    nameof(Role.ADMIN) => null,
                    nameof(Role.PM) => p => p.PMUserID == currentUser.Id,
                    nameof(Role.WAREHOUSE_MANAGER) => p =>
                        p.MaterialRequests.Any(r => r.WarehouseId.HasValue && r.Warehouse!.ManagerId == currentUser.Id) ||
                        p.PurchaseOrders.Any(o => o.Warehouse.ManagerId == currentUser.Id),
                    _ => p => false
                };
                var projects = await _unitOfWork.Projects.GetAllAsync(
                    filter: accessFilter,
                    include: query => query
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Tasks)
                        .Include(p => p.PurchaseOrders).ThenInclude(o => o.OrderLineItems)
                        .Include(p => p.AIAlerts)
                );

                var response = _mapper.Map<List<ProjectResponse>>(projects);

                return apiResponse.SetOk(response);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve projects.");
            }
        }

        public async Task<ApiResponse> GetProjectByIdAsync(int id)
        {
            var apiResponse = new ApiResponse();

            try
            {
                var project = (await _unitOfWork.Projects.GetAllAsync(
                    filter: p => p.ProjectId == id,
                    include: query => query
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Tasks)
                        .Include(p => p.PurchaseOrders).ThenInclude(o => o.OrderLineItems)
                        .Include(p => p.AIAlerts)
                )).FirstOrDefault();

                if (project == null)
                {
                    return apiResponse.SetNotFound("Project not found or has been deleted.");
                }
                if (!await CanReadProjectAsync(id, project.PMUserID))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project.");

                var response = _mapper.Map<ProjectResponse>(project);

                return apiResponse.SetOk(response);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve the project.");
            }
        }

        public async Task<ApiResponse> ImportProjectFromWordAsync(IFormFile file)
        {
            var apiResponse = new ApiResponse();

            const long maxWordSize = 10 * 1024 * 1024;
            if (file == null || file.Length == 0)
                return apiResponse.SetBadRequest("Upload a valid Word (.docx) file.");
            if (file.Length > maxWordSize || !string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
                return apiResponse.SetBadRequest(message: "The project import must be a .docx file no larger than 10 MB.");

            // Áp dụng Giao dịch (Transaction) để bảo vệ dữ liệu khi bóc tách file
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var currentUser = _claimService.GetUserClaim();
                if (!string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only project managers may import projects.");
                }
                int pmUserId = currentUser.Id;

                var pmAccount = await _unitOfWork.UserAccounts.GetAsync(u => u.Id == pmUserId);
                if (pmAccount == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return apiResponse.SetNotFound("The selected project-manager account does not exist or is invalid.");
                }

                string projectName = "";
                string address = "";
                decimal totalBudget = 0;
                string currency = "VND";
                DateTime startDate = DateTime.UtcNow;
                DateTime baselineStart = DateTime.UtcNow;
                DateTime baselineEnd = DateTime.UtcNow.AddMonths(6);

                using (var stream = file.OpenReadStream())
                {
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false))
                    {
                        var body = wordDoc.MainDocumentPart?.Document?.Body
                            ?? throw new ArgumentNullException(nameof(file));
                        var paragraphs = body.Descendants<WordXml.Paragraph>();

                        foreach (var para in paragraphs)
                        {
                            string text = para.InnerText.Trim();
                            if (string.IsNullOrEmpty(text)) continue;

                            if (text.StartsWith("Tên dự án:", StringComparison.OrdinalIgnoreCase))
                                projectName = text.Replace("Tên dự án:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Địa điểm:", StringComparison.OrdinalIgnoreCase))
                                address = text.Replace("Địa điểm:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Ngân sách tổng:", StringComparison.OrdinalIgnoreCase))
                            {
                                var value = text.Replace("Ngân sách tổng:", "", StringComparison.OrdinalIgnoreCase).Trim();
                                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out totalBudget))
                                    throw new FormatException("Ngân sách tổng must use invariant numeric format, for example 1250000.50.");
                            }

                            else if (text.StartsWith("Tiền tệ:", StringComparison.OrdinalIgnoreCase))
                                currency = text.Replace("Tiền tệ:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Ngày thực tế bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                startDate = ParseImportDate(text.Replace("Ngày thực tế bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim());

                            else if (text.StartsWith("Ngày kế hoạch bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                baselineStart = ParseImportDate(text.Replace("Ngày kế hoạch bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim());

                            else if (text.StartsWith("Ngày kế hoạch kết thúc:", StringComparison.OrdinalIgnoreCase))
                                baselineEnd = ParseImportDate(text.Replace("Ngày kế hoạch kết thúc:", "", StringComparison.OrdinalIgnoreCase).Trim());
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(projectName))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return apiResponse.SetBadRequest("The document is empty or invalid. The required 'Tên dự án:' line was not found.");
                }
                if (projectName.Length > 200 || address.Length > 500 || totalBudget < 0 || baselineEnd < baselineStart || startDate > baselineEnd)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return apiResponse.SetBadRequest(message: "The imported project contains invalid lengths, a negative budget, or an invalid baseline date range.");
                }

                var project = new Project
                {
                    ProjectName = projectName,
                    Address = string.IsNullOrWhiteSpace(address) ? null : address,
                    Status = ProjectStatus.PLANNING,
                    StartDate = startDate,
                    BaselineStart = baselineStart,
                    BaselineEnd = baselineEnd,
                    TotalProjectBudget = totalBudget,
                    Currency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency,
                    PMUserID = pmUserId
                };

                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangeAsync();

                await _unitOfWork.CommitTransactionAsync();

                var resultResponse = _mapper.Map<ProjectResponse>(project);
                return apiResponse.SetOk(resultResponse);
            }
            catch (ArgumentNullException)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return apiResponse.SetBadRequest("The Word document does not contain all required project fields.");
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return InternalError("Unable to import the Word document.");
            }
        }

        private static DateTime ParseImportDate(string value)
        {
            var formats = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "O" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;
            throw new FormatException($"Invalid date '{value}'. Use yyyy-MM-dd.");
        }

        public async Task<ApiResponse> AssignMaterialRequirementToTaskAsync(int taskId, CreateTaskMaterialRequirementRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                // KIỂM TRA ĐIỀU KIỆN BIÊN: Chặn các giá trị số lượng bất hợp pháp (<= 0) từ phía Client
                if (request.GrossQuantityRequired <= 0)
                {
                    return apiResponse.SetBadRequest("Planned material quantity must be greater than zero.");
                }

                var taskItem = await _unitOfWork.TaskItems.GetByIdAsync(taskId);
                if (taskItem == null)
                    return apiResponse.SetNotFound($"Task {taskId} was not found.");
                var project = await _unitOfWork.Projects.GetByIdAsync(taskItem.ProjectId);
                var currentUser = _claimService.GetUserClaim();
                if (project == null || !string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || project.PMUserID != currentUser.Id)
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only edit planned materials for a project you manage.");
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                    return apiResponse.SetConflict("Closed projects cannot accept material-plan changes.");
                if (taskItem.Status is cpms_Domain.Models.TaskStatus.COMPLETED or cpms_Domain.Models.TaskStatus.CANCELLED)
                    return apiResponse.SetConflict("Closed tasks cannot accept material-plan changes.");
                var downstreamRequest = await _unitOfWork.MaterialRequests.GetAsync(r =>
                    r.TaskId == taskId &&
                    r.Status != MaterialRequestStatuses.Rejected &&
                    r.Status != MaterialRequestStatuses.Released &&
                    r.Status != MaterialRequestStatuses.Cancelled);
                if (downstreamRequest != null)
                    return apiResponse.SetConflict("Material planning is locked after a material request has entered fulfillment.");

                MaterialVariant? variant;
                if (request.VariantId != 0)
                    variant = await _unitOfWork.MaterialVariants.GetByIdAsync(request.VariantId);
                else
                {
                    var candidates = await _unitOfWork.MaterialVariants.GetAllAsync(v => v.MaterialId == request.MaterialId && v.IsActive);
                    variant = candidates.Count == 1 ? candidates[0] : null;
                }
                if (variant == null || !variant.IsActive)
                    return apiResponse.SetNotFound(message: "Material variant not found.");

                var existingRequirement = await _unitOfWork.TaskMaterialRequirements
                    .GetAsync(r => r.TaskId == taskId && r.VariantId == variant.VariantId);

                if (existingRequirement != null)
                {
                    existingRequirement.GrossQuantityRequired = request.GrossQuantityRequired;
                    _unitOfWork.TaskMaterialRequirements.Update(existingRequirement);
                }
                else
                {
                    existingRequirement = new TaskMaterialRequirement
                    {
                        TaskId = taskId,
                        VariantId = variant.VariantId,
                        GrossQuantityRequired = request.GrossQuantityRequired
                    };
                    await _unitOfWork.TaskMaterialRequirements.AddAsync(existingRequirement);
                }

                await _unitOfWork.SaveChangeAsync();

                var responseResult = _mapper.Map<TaskMaterialResponse>(existingRequirement);
                return apiResponse.SetOk(responseResult);
            }
            catch (Exception)
            {
                return InternalError("Unable to assign the material requirement.");
            }
        }

        public async Task<ApiResponse> GetMaterialRequirementsByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Project not found.");
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                    return apiResponse.SetConflict("MRP cannot be recalculated for a closed project.");
                if (!await CanReadProjectAsync(projectId, project.PMUserID))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project's material requirements.");

                var projectRequirements = await _unitOfWork.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId,
                    include: query => query
                        .Include(r => r.Variant)
                            .ThenInclude(v => v.Material)
                        .Include(r => r.TaskItem)
                );

                var response = _mapper.Map<List<TaskMaterialResponse>>(projectRequirements);
                return apiResponse.SetOk(response);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve material requirements.");
            }
        }

        public async Task<ApiResponse> CalculateMRPForProjectAsync(int projectId, int? warehouseId = null)
        {
            var apiResponse = new ApiResponse();
            var versionTransactionStarted = false;
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Project not found.");
                var currentUser = _claimService.GetUserClaim();
                Warehouse? selectedWarehouse = null;
                if (!warehouseId.HasValue)
                    return apiResponse.SetBadRequest("warehouseId is required so inventory from another warehouse cannot hide a local shortage.");
                if (warehouseId.HasValue)
                {
                    selectedWarehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId.Value);
                    if (selectedWarehouse == null) return apiResponse.SetNotFound(message: "Warehouse not found.");
                }
                if (IsRole(currentUser, Role.PM) && project.PMUserID != currentUser.Id)
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only calculate MRP for a project you manage.");
                if (IsRole(currentUser, Role.WAREHOUSE_MANAGER) &&
                    (selectedWarehouse == null || selectedWarehouse.ManagerId != currentUser.Id))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Warehouse managers must select a warehouse they manage.");
                if (!IsRole(currentUser, Role.ADMIN) && !IsRole(currentUser, Role.PM) && !IsRole(currentUser, Role.WAREHOUSE_MANAGER))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "This role cannot calculate MRP.");

                var requirements = await _unitOfWork.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId
                               && r.TaskItem.Status != cpms_Domain.Models.TaskStatus.COMPLETED
                               && r.TaskItem.Status != cpms_Domain.Models.TaskStatus.CANCELLED
                               && r.TaskItem.Status != cpms_Domain.Models.TaskStatus.REJECTED
                               && r.TaskItem.ActualProgressPct < 100,
                    include: query => query
                        .Include(r => r.Variant)
                            .ThenInclude(v => v.Material)
                        .Include(r => r.TaskItem)
                );

                var requirementsList = requirements.ToList();

                if (!requirementsList.Any())
                    return apiResponse.SetOk(new List<MRPCalculationResponse>());

                var activeTaskIds = requirementsList.Select(r => r.TaskId).Distinct().ToList();

                var issuedItems = await _unitOfWork.MaterialRequisitions.GetAllAsync(
                    filter: r => r.MaterialRequest.ProjectId == projectId &&
                                 r.MaterialRequest.TaskId.HasValue &&
                                 activeTaskIds.Contains(r.MaterialRequest.TaskId.Value) &&
                                 r.IssuedQuantity > 0,
                    include: query => query.Include(r => r.MaterialRequest));
                var issuedByVariant = issuedItems
                    .GroupBy(r => r.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));
                var returnedItems = await _unitOfWork.MaterialReturns.GetAllAsync(
                    filter: r => r.MaterialRequest.ProjectId == projectId &&
                                 r.MaterialRequest.TaskId.HasValue &&
                                 activeTaskIds.Contains(r.MaterialRequest.TaskId.Value),
                    include: query => query.Include(r => r.MaterialRequest));
                var returnedByVariant = returnedItems
                    .GroupBy(r => r.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

                var projectReservations = await _unitOfWork.InventoryReservations.GetAllAsync(
                    filter: r => r.MaterialRequest.ProjectId == projectId &&
                                 r.Status == InventoryReservationStatuses.Active &&
                                 (!warehouseId.HasValue || r.InventoryRecord.WarehouseId == warehouseId.Value),
                    include: query => query.Include(r => r.RequestItem).Include(r => r.InventoryRecord));
                var reservedForProjectByVariant = projectReservations
                    .GroupBy(r => r.RequestItem.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

                var projectOpenOrderLines = await _unitOfWork.OrderLineItems.GetAllAsync(
                    filter: line => line.PurchaseOrder.ProjectId == projectId &&
                                    (line.PurchaseOrder.Status == PurchaseOrderStatus.APPROVED ||
                                     line.PurchaseOrder.Status == PurchaseOrderStatus.PROCESSING ||
                                     line.PurchaseOrder.Status == PurchaseOrderStatus.SHIPPED ||
                                     line.PurchaseOrder.Status == PurchaseOrderStatus.PARTIALLY_RECEIVED) &&
                                    line.ReceivedQuantity + line.DamagedQuantity + line.MissingQuantity < line.Quantity &&
                                    (!warehouseId.HasValue || line.PurchaseOrder.WarehouseId == warehouseId.Value),
                    include: query => query.Include(line => line.PurchaseOrder));
                var openOrderForProjectByVariant = projectOpenOrderLines
                    .GroupBy(line => line.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(line =>
                        line.Quantity - line.ReceivedQuantity - line.DamagedQuantity - line.MissingQuantity));

                var grossRequirementsGroup = requirementsList
                    .GroupBy(r => new
                    {
                        r.VariantId,
                        r.Variant.MaterialId,
                        MaterialName = r.Variant.Material.MaterialName,
                        VariantName = r.Variant.VariantName,
                        Unit = r.Variant.Unit
                    })
                    .Select(g => new
                    {
                        g.Key.VariantId,
                        g.Key.MaterialId,
                        g.Key.MaterialName,
                        g.Key.VariantName,
                        g.Key.Unit,
                        TotalGross = g.Sum(r => r.GrossQuantityRequired),
                        Issued = Math.Min(g.Sum(r => r.GrossQuantityRequired), Math.Max(0,
                            issuedByVariant.GetValueOrDefault(g.Key.VariantId) - returnedByVariant.GetValueOrDefault(g.Key.VariantId))),
                        RemainingGross = Math.Max(0, g.Sum(r => r.GrossQuantityRequired) - Math.Max(0,
                            issuedByVariant.GetValueOrDefault(g.Key.VariantId) - returnedByVariant.GetValueOrDefault(g.Key.VariantId))),
                        EarliestNeedDate = g.Min(r => r.TaskItem.BaselineStart)
                    }).ToList();

                // LẤY DANH SÁCH ID VẬT TƯ ĐỂ TRUY VẤN TRÚNG ĐÍCH
                var requiredVariantIds = grossRequirementsGroup.Select(g => g.VariantId).Distinct().ToList();

                // TỐI ƯU HÓA TRUY VẤN: Chỉ lấy tồn kho của các Vật tư có trong danh sách yêu cầu
                var currentInventories = await _unitOfWork.Inventories.GetAllAsync(
                    filter: i => requiredVariantIds.Contains(i.VariantId) &&
                                 (!warehouseId.HasValue || i.WarehouseId == warehouseId.Value)
                );
                var inventoriesList = currentInventories.ToList();
                var alternateInventories = await _unitOfWork.Inventories.GetAllAsync(
                    filter: i => requiredVariantIds.Contains(i.VariantId) && i.WarehouseId != warehouseId.Value);

                var mrpResultList = new List<MRPCalculationResponse>();

                foreach (var gross in grossRequirementsGroup)
                {
                    // Lọc dữ liệu trên danh sách đã được thu nhỏ trong bộ nhớ RAM
                    decimal currentStock = inventoriesList
                        .Where(i => i.VariantId == gross.VariantId)
                        .Sum(i => i.QuantityOnHand);

                    decimal reserved = inventoriesList
                        .Where(i => i.VariantId == gross.VariantId)
                        .Sum(i => i.ReservedQuantity);

                    decimal quarantined = inventoriesList
                        .Where(i => i.VariantId == gross.VariantId)
                        .Sum(i => i.QuarantineQuantity);

                    decimal available = currentStock - reserved - quarantined;
                    if (available < 0) available = 0;

                    decimal reservedForProject = reservedForProjectByVariant.GetValueOrDefault(gross.VariantId);
                    decimal onOrder = openOrderForProjectByVariant.GetValueOrDefault(gross.VariantId);

                    decimal netRequired = gross.RemainingGross - available - reservedForProject - onOrder;
                    if (netRequired < 0) netRequired = 0;

                    var remainingForTransfer = netRequired;
                    var transferRecommendations = alternateInventories
                        .Where(i => i.VariantId == gross.VariantId && i.QuantityOnHand - i.ReservedQuantity - i.QuarantineQuantity > 0)
                        .OrderByDescending(i => i.QuantityOnHand - i.ReservedQuantity - i.QuarantineQuantity)
                        .Select(i =>
                        {
                            var suggested = Math.Min(remainingForTransfer,
                                Math.Max(0, i.QuantityOnHand - i.ReservedQuantity - i.QuarantineQuantity));
                            remainingForTransfer -= suggested;
                            return new MRPTransferRecommendation
                            {
                                SourceWarehouseId = i.WarehouseId,
                                DestinationWarehouseId = warehouseId.Value,
                                VariantId = gross.VariantId,
                                SuggestedQuantity = suggested
                            };
                        })
                        .Where(x => x.SuggestedQuantity > 0)
                        .ToList();

                    mrpResultList.Add(new MRPCalculationResponse
                    {
                        VariantId = gross.VariantId,
                        WarehouseId = warehouseId,
                        InventoryScope = "WAREHOUSE",
                        MaterialId = gross.MaterialId,
                        MaterialName = gross.MaterialName,
                        VariantName = gross.VariantName,
                        Unit = gross.Unit,
                        TotalGrossRequired = gross.TotalGross,
                        IssuedToProjectTasks = gross.Issued,
                        RemainingGrossRequired = gross.RemainingGross,
                        CurrentInventory = currentStock,
                        ReservedQuantity = reserved,
                        AvailableQuantity = available,
                        OnOrderQuantity = onOrder,
                        NetQuantityRequired = netRequired,
                        EarliestStartDate = gross.EarliestNeedDate,
                        TransferRecommendations = transferRecommendations
                    });
                }

                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                versionTransactionStarted = true;
                var previousRuns = await _unitOfWork.MrpPlanningRuns.GetAllAsync(
                    r => r.ProjectId == projectId && r.WarehouseId == warehouseId.Value);
                var run = new MrpPlanningRun
                {
                    ProjectId = projectId,
                    WarehouseId = warehouseId.Value,
                    Version = previousRuns.Count == 0 ? 1 : previousRuns.Max(r => r.Version) + 1,
                    CalculatedAt = DateTime.UtcNow,
                    CalculatedByUserId = currentUser.Id,
                    SnapshotJson = JsonSerializer.Serialize(mrpResultList),
                    TransferRecommendationsJson = JsonSerializer.Serialize(mrpResultList.SelectMany(x => x.TransferRecommendations))
                };
                await _unitOfWork.MrpPlanningRuns.AddAsync(run);
                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
                versionTransactionStarted = false;
                foreach (var item in mrpResultList)
                {
                    item.PlanningRunId = run.RunId;
                    item.PlanningVersion = run.Version;
                }

                return apiResponse.SetOk(mrpResultList);
            }
            catch (DbUpdateException)
            {
                if (versionTransactionStarted) await _unitOfWork.RollbackTransactionAsync();
                return apiResponse.SetConflict("Another MRP run was created concurrently. Run the calculation again.");
            }
            catch (Exception)
            {
                if (versionTransactionStarted) await _unitOfWork.RollbackTransactionAsync();
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to calculate MRP.");
            }
        }

        public async Task<ApiResponse> GetLatestMRPForProjectAsync(int projectId, int warehouseId)
        {
            if (warehouseId <= 0) return new ApiResponse().SetBadRequest("warehouseId is required.");
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
            if (project == null) return new ApiResponse().SetNotFound("Project not found.");
            var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(warehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound("Warehouse not found.");
            var user = _claimService.GetUserClaim();
            if (IsRole(user, Role.PM) && project.PMUserID != user.Id)
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only view MRP runs for a project you manage.");
            if (IsRole(user, Role.WAREHOUSE_MANAGER) && warehouse.ManagerId != user.Id)
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only view MRP runs for a warehouse you manage.");
            if (!IsRole(user, Role.ADMIN) && !IsRole(user, Role.PM) && !IsRole(user, Role.WAREHOUSE_MANAGER))
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "This role cannot view MRP runs.");

            var runs = await _unitOfWork.MrpPlanningRuns.GetAllAsync(run =>
                run.ProjectId == projectId && run.WarehouseId == warehouseId);
            var latest = runs.OrderByDescending(run => run.Version).FirstOrDefault();
            if (latest == null) return new ApiResponse().SetNotFound("No MRP run exists for this project and warehouse.");
            var items = JsonSerializer.Deserialize<List<MRPCalculationResponse>>(latest.SnapshotJson) ?? new();
            foreach (var item in items)
            {
                item.PlanningRunId = latest.RunId;
                item.PlanningVersion = latest.Version;
            }
            return new ApiResponse().SetOk(new
            {
                PlanningRunId = latest.RunId,
                PlanningVersion = latest.Version,
                latest.CalculatedAt,
                latest.CalculatedByUserId,
                Items = items
            });
        }
        public async Task<ApiResponse> AdjustProjectBudgetAsync(AdjustBudgetRequest request)
        {
            var apiResponse = new ApiResponse();
            var transactionStarted = false;
            try
            {
                if (request.Amount == 0 || string.IsNullOrWhiteSpace(request.Reason))
                    return apiResponse.SetBadRequest("A non-zero amount and a reason are required.");
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                transactionStarted = true;
                var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId);
                if (project == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    transactionStarted = false;
                    return apiResponse.SetNotFound("Project not found.");
                }
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    transactionStarted = false;
                    return apiResponse.SetConflict("A closed project's budget cannot be changed.");
                }

                decimal oldBudget = project.TotalProjectBudget;
                var newBudget = oldBudget + request.Amount;
                var committedOrders = await _unitOfWork.PurchaseOrders.GetAllAsync(
                    p => p.ProjectId == request.ProjectId && p.Status != PurchaseOrderStatus.REJECTED &&
                         p.Status != PurchaseOrderStatus.CANCELLED);
                var committedAmount = committedOrders.Sum(p => p.TotalAmount);
                var tasks = await _unitOfWork.TaskItems.GetAllAsync(t => t.ProjectId == request.ProjectId);
                var plannedTaskAmount = tasks
                    .Where(t => t.Status is not (cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED))
                    .Sum(t => t.PlannedBudget);
                var reportedActualAmount = tasks.Sum(t => t.ActualCost);
                var minimumSupportedBudget = Math.Max(committedAmount, Math.Max(plannedTaskAmount, reportedActualAmount));
                if (newBudget < 0 || newBudget < minimumSupportedBudget)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    transactionStarted = false;
                    return apiResponse.SetConflict(message:
                        $"The adjusted budget cannot be negative or below the current planning/commitment floor of {minimumSupportedBudget}.");
                }

                // Cập nhật ngân sách
                project.TotalProjectBudget = newBudget;

                var history = new ProjectBudgetHistory
                {
                    ProjectId = request.ProjectId,
                    AmountChanged = request.Amount,
                    PreviousBudget = oldBudget,
                    NewBudget = project.TotalProjectBudget,
                    Reason = request.Reason,
                    UpdatedByUserId = _claimService.GetUserClaim().Id,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.ProjectBudgetHistories.AddAsync(history);

                _unitOfWork.Projects.Update(project);

                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
                transactionStarted = false;

                // Mapping sang Response
                var response = _mapper.Map<ProjectBudgetHistoryResponse>(history);

                // Vì History không có Currency nên gán từ Project
                response.Currency = project.Currency;

                return apiResponse.SetOk(response);
            }
            catch (Exception)
            {
                if (transactionStarted) await _unitOfWork.RollbackTransactionAsync();
                return InternalError("Unable to adjust the project budget.");
            }
        }

        public async Task<ApiResponse> GetBudgetHistoriesByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null) return apiResponse.SetNotFound("Project not found.");
                var currentUser = _claimService.GetUserClaim();
                if (!IsRole(currentUser, Role.ADMIN) && (!IsRole(currentUser, Role.PM) || project.PMUserID != currentUser.Id))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project's budget history.");
                // 1. Lấy dữ liệu từ Database
                var histories = await _unitOfWork.ProjectBudgetHistories.GetAllAsync(h => h.ProjectId == projectId);

                var result = _mapper.Map<List<ProjectBudgetHistoryResponse>>(
                    histories.OrderByDescending(h => h.CreatedAt).ToList()
                );

                // 3. Trả về kết quả
                return apiResponse.SetOk(result);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve the project budget history.");
            }
        }

        public async Task<ApiResponse> UpdateProjectAsync(int projectId, UpdateProjectRequest request)
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
            if (project == null) return new ApiResponse().SetNotFound("Project not found.");
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.PM) || project.PMUserID != user.Id)
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only the owning project manager may update this project.");
            if (!MatchesRowVersion(project.RowVersion, request.RowVersion))
                return new ApiResponse().SetConflict("Project changed. Reload and retry.");
            var tasks = await _unitOfWork.TaskItems.GetAllAsync(t => t.ProjectId == projectId);
            if (tasks.Any(task => task.BaselineStart < request.BaselineStart || task.BaselineEnd > request.BaselineEnd))
                return new ApiResponse().SetConflict("Project dates cannot exclude an existing task. Reschedule the affected tasks first.");
            try
            {
                project.UpdatePlan(request.ProjectName, request.Address, request.StartDate, request.BaselineStart, request.BaselineEnd);
                await _unitOfWork.SaveChangeAsync();
                return await GetProjectByIdAsync(projectId);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return new ApiResponse().SetConflict(ex.Message);
            }
        }

        public async Task<ApiResponse> ChangeProjectStatusAsync(int projectId, string action, ProjectLifecycleRequest request)
        {
            var normalizedAction = action.Trim().ToLowerInvariant();
            var closureTransaction = normalizedAction is "cancel" or "complete";
            if (closureTransaction)
                await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            async Task<ApiResponse> Abort(ApiResponse response)
            {
                if (closureTransaction) await _unitOfWork.RollbackTransactionAsync();
                return response;
            }

            try
            {
                var project = await _unitOfWork.Projects.GetAsync(p => p.ProjectId == projectId,
                    query => query.Include(p => p.Tasks));
                if (project == null) return await Abort(new ApiResponse().SetNotFound("Project not found."));
                var user = _claimService.GetUserClaim();
                if (!IsRole(user, Role.ADMIN) && (!IsRole(user, Role.PM) || project.PMUserID != user.Id))
                    return await Abort(new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You cannot change this project."));
                if (!MatchesRowVersion(project.RowVersion, request.RowVersion))
                    return await Abort(new ApiResponse().SetConflict("Project changed. Reload and retry."));

                switch (normalizedAction)
                {
                    case "start": project.Start(DateTime.UtcNow); break;
                    case "pause": project.Pause(); break;
                    case "cancel":
                        var cancellationBlocker = await GetProjectClosureBlockerAsync(projectId);
                        if (cancellationBlocker != null)
                            return await Abort(new ApiResponse().SetConflict(cancellationBlocker));
                        project.Cancel();
                        break;
                    case "reopen": project.Reopen(); break;
                    case "complete":
                        var completionBlocker = await GetProjectClosureBlockerAsync(projectId);
                        if (completionBlocker != null)
                            return await Abort(new ApiResponse().SetConflict(completionBlocker));
                        project.Complete(project.Tasks.Any(t => t.Status == cpms_Domain.Models.TaskStatus.COMPLETED) &&
                            project.Tasks.All(t => t.Status is cpms_Domain.Models.TaskStatus.COMPLETED or
                                cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED));
                        break;
                    default: return await Abort(new ApiResponse().SetBadRequest("Supported project actions are start, pause, cancel, reopen, and complete."));
                }
                await _unitOfWork.SaveChangeAsync();
                if (closureTransaction) await _unitOfWork.CommitTransactionAsync();
                return new ApiResponse().SetOk(new { project.ProjectId, Status = project.Status.ToString(), RowVersion = Convert.ToBase64String(project.RowVersion) });
            }
            catch (InvalidOperationException ex)
            {
                if (closureTransaction) await _unitOfWork.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(ex.Message);
            }
            catch
            {
                if (closureTransaction) await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ApiResponse> ReassignProjectManagerAsync(int projectId, ReassignProjectManagerRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsRole(user, Role.ADMIN))
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Administrator access is required.");
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
            if (project == null) return new ApiResponse().SetNotFound("Project not found.");
            if (!MatchesRowVersion(project.RowVersion, request.RowVersion))
                return new ApiResponse().SetConflict("Project changed. Reload and retry.");
            var manager = await _unitOfWork.UserAccounts.GetByIdAsync(request.ProjectManagerUserId);
            if (manager == null || manager.Role != Role.PM || manager.IsEmailVerified != true)
                return new ApiResponse().SetBadRequest("The new manager must be a verified PM.");
            project.PMUserID = manager.Id;
            await _unitOfWork.SaveChangeAsync();
            return await GetProjectByIdAsync(projectId);
        }

        private static bool MatchesRowVersion(byte[] current, string supplied) =>
            !string.IsNullOrWhiteSpace(supplied) && Convert.ToBase64String(current).Equals(supplied, StringComparison.Ordinal);

        private async Task<string?> GetProjectClosureBlockerAsync(int projectId)
        {
            var openRequest = await _unitOfWork.MaterialRequests.GetAsync(request =>
                request.ProjectId == projectId &&
                (request.Status == MaterialRequestStatuses.Pending ||
                 request.Status == MaterialRequestStatuses.Approved ||
                 request.Status == MaterialRequestStatuses.PartiallyApproved ||
                 request.Status == MaterialRequestStatuses.PartiallyIssued));
            if (openRequest != null)
                return "Reject, cancel, release, or finish open material requests before closing the project.";

            var activeReservation = await _unitOfWork.InventoryReservations.GetAsync(reservation =>
                reservation.MaterialRequest.ProjectId == projectId && reservation.Status == InventoryReservationStatuses.Active);
            if (activeReservation != null)
                return "Release or issue active inventory reservations before closing the project.";

            var openOrder = await _unitOfWork.PurchaseOrders.GetAsync(order => order.ProjectId == projectId &&
                (order.Status == PurchaseOrderStatus.PENDING || order.Status == PurchaseOrderStatus.APPROVED ||
                 order.Status == PurchaseOrderStatus.PROCESSING || order.Status == PurchaseOrderStatus.SHIPPED ||
                 order.Status == PurchaseOrderStatus.PARTIALLY_RECEIVED));
            if (openOrder != null)
                return "Reject, cancel, or finish open purchase orders before closing the project.";

            var pendingReport = await _unitOfWork.ProgressReports.GetAsync(report =>
                report.Task.ProjectId == projectId && report.Status == ProgressReportStatus.PENDING);
            return pendingReport == null
                ? null
                : "Approve or reject pending progress reports before closing the project.";
        }

        private async Task<bool> CanReadProjectAsync(int projectId, int projectManagerId)
        {
            var currentUser = _claimService.GetUserClaim();
            if (IsRole(currentUser, Role.ADMIN)) return true;
            if (IsRole(currentUser, Role.PM)) return projectManagerId == currentUser.Id;
            if (!IsRole(currentUser, Role.WAREHOUSE_MANAGER)) return false;

            var linkedRequest = await _unitOfWork.MaterialRequests.GetAsync(r =>
                r.ProjectId == projectId && r.WarehouseId.HasValue && r.Warehouse!.ManagerId == currentUser.Id);
            if (linkedRequest != null) return true;
            var linkedOrder = await _unitOfWork.PurchaseOrders.GetAsync(o =>
                o.ProjectId == projectId && o.Warehouse.ManagerId == currentUser.Id);
            return linkedOrder != null;
        }

        private static bool IsRole(ClaimDTO claim, Role role) =>
            string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
        private static ApiResponse InternalError(string message) =>
            new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, message);
    }
}
