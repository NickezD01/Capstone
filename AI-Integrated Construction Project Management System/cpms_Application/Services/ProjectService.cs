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
using System;
using System.Collections.Generic;
using System.IO;
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
                if (request.TotalProjectBudget < 0 || request.BaselineEnd < request.BaselineStart)
                    return apiResponse.SetBadRequest("Project budget cannot be negative and baseline end must not precede baseline start.");
                // Kiểm tra PM có tồn tại không
                var pm = await _unitOfWork.UserAccounts.GetByIdAsync(request.PMUserID);

                if (pm == null)
                {
                    return apiResponse.SetNotFound("Project Manager không tồn tại.");
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
                        .Include(p => p.AIAlerts)
                )).FirstOrDefault();

                if (createdProject == null)
                {
                    return apiResponse.SetNotFound("Không tìm thấy dự án vừa tạo.");
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

            if (file == null || file.Length == 0)
                return apiResponse.SetBadRequest("Vui lòng tải lên một file Word (.docx) hợp lệ.");

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
                    return apiResponse.SetNotFound("Tài khoản quản lý dự án (PMUserID) không tồn tại hoặc không hợp lệ.");
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
                                decimal.TryParse(text.Replace("Ngân sách tổng:", "", StringComparison.OrdinalIgnoreCase).Trim(), out totalBudget);

                            else if (text.StartsWith("Tiền tệ:", StringComparison.OrdinalIgnoreCase))
                                currency = text.Replace("Tiền tệ:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Ngày thực tế bắt đầu:", StringComparison.OrdinalIgnoreCase) &&
                                     DateTime.TryParse(text.Replace("Ngày thực tế bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out var parsedStart))
                                startDate = parsedStart;

                            else if (text.StartsWith("Ngày kế hoạch bắt đầu:", StringComparison.OrdinalIgnoreCase) &&
                                     DateTime.TryParse(text.Replace("Ngày kế hoạch bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out var parsedBaselineStart))
                                baselineStart = parsedBaselineStart;

                            else if (text.StartsWith("Ngày kế hoạch kết thúc:", StringComparison.OrdinalIgnoreCase) &&
                                     DateTime.TryParse(text.Replace("Ngày kế hoạch kết thúc:", "", StringComparison.OrdinalIgnoreCase).Trim(), out var parsedBaselineEnd))
                                baselineEnd = parsedBaselineEnd;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(projectName))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return apiResponse.SetBadRequest("File văn bản sai cấu trúc hoặc trống. Không tìm thấy dòng chứa tiêu đề 'Tên dự án:'.");
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

        public async Task<ApiResponse> AssignMaterialRequirementToTaskAsync(int taskId, CreateTaskMaterialRequirementRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                // KIỂM TRA ĐIỀU KIỆN BIÊN: Chặn các giá trị số lượng bất hợp pháp (<= 0) từ phía Client
                if (request.GrossQuantityRequired <= 0)
                {
                    return apiResponse.SetBadRequest("Số lượng định mức vật tư yêu cầu phải lớn hơn 0.");
                }

                var taskItem = await _unitOfWork.TaskItems.GetByIdAsync(taskId);
                if (taskItem == null)
                    return apiResponse.SetNotFound($"Không tìm thấy đầu việc (Task) với ID = {taskId}");
                var project = await _unitOfWork.Projects.GetByIdAsync(taskItem.ProjectId);
                var currentUser = _claimService.GetUserClaim();
                if (project == null || !string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || project.PMUserID != currentUser.Id)
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only edit planned materials for a project you manage.");

                var variant = request.VariantId != 0
                    ? await _unitOfWork.MaterialVariants.GetByIdAsync(request.VariantId)
                    : await _unitOfWork.MaterialVariants.GetAsync(v => v.MaterialId == request.MaterialId && v.IsActive);
                if (variant == null)
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
                    return apiResponse.SetNotFound("Dự án không tồn tại.");
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
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Dự án không tồn tại.");
                var currentUser = _claimService.GetUserClaim();
                Warehouse? selectedWarehouse = null;
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
                              && r.TaskItem.ActualProgressPct < 100,
                    include: query => query
                        .Include(r => r.Variant)
                            .ThenInclude(v => v.Material)
                        .Include(r => r.TaskItem)
                );

                var requirementsList = requirements.ToList();

                if (!requirementsList.Any())
                    return apiResponse.SetOk(new List<MRPCalculationResponse>());

                var issuedItems = await _unitOfWork.MaterialRequisitions.GetAllAsync(
                    filter: r => r.MaterialRequest.ProjectId == projectId && r.IssuedQuantity > 0,
                    include: query => query.Include(r => r.MaterialRequest));
                var issuedByVariant = issuedItems
                    .GroupBy(r => r.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));

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
                                    (line.PurchaseOrder.Status == PurchaseOrderStatus.PENDING ||
                                     line.PurchaseOrder.Status == PurchaseOrderStatus.APPROVED) &&
                                    line.ReceivedQuantity < line.Quantity &&
                                    (!warehouseId.HasValue || line.PurchaseOrder.WarehouseId == warehouseId.Value),
                    include: query => query.Include(line => line.PurchaseOrder));
                var openOrderForProjectByVariant = projectOpenOrderLines
                    .GroupBy(line => line.VariantId)
                    .ToDictionary(g => g.Key, g => g.Sum(line => line.Quantity - line.ReceivedQuantity));

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
                        Issued = Math.Min(g.Sum(r => r.GrossQuantityRequired), issuedByVariant.GetValueOrDefault(g.Key.VariantId)),
                        RemainingGross = Math.Max(0, g.Sum(r => r.GrossQuantityRequired) - issuedByVariant.GetValueOrDefault(g.Key.VariantId)),
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

                    decimal available = currentStock - reserved;
                    if (available < 0) available = 0;

                    decimal reservedForProject = reservedForProjectByVariant.GetValueOrDefault(gross.VariantId);
                    decimal onOrder = openOrderForProjectByVariant.GetValueOrDefault(gross.VariantId);

                    decimal netRequired = gross.RemainingGross - available - reservedForProject - onOrder;
                    if (netRequired < 0) netRequired = 0;

                    mrpResultList.Add(new MRPCalculationResponse
                    {
                        VariantId = gross.VariantId,
                        WarehouseId = warehouseId,
                        InventoryScope = warehouseId.HasValue ? "WAREHOUSE" : "ALL_WAREHOUSES",
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
                        EarliestStartDate = gross.EarliestNeedDate
                    });
                }

                return apiResponse.SetOk(mrpResultList);
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to calculate MRP.");
            }
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
                    return apiResponse.SetNotFound("Không tìm thấy dự án.");
                }

                decimal oldBudget = project.TotalProjectBudget;
                var newBudget = oldBudget + request.Amount;
                var committedOrders = await _unitOfWork.PurchaseOrders.GetAllAsync(
                    p => p.ProjectId == request.ProjectId && p.Status != PurchaseOrderStatus.REJECTED);
                var committedAmount = committedOrders.Sum(p => p.TotalAmount);
                if (newBudget < 0 || newBudget < committedAmount)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    transactionStarted = false;
                    return apiResponse.SetConflict(message: "The adjusted budget cannot be negative or below committed purchase orders.");
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
