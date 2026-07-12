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
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetAllProjectsAsync()
        {
            var apiResponse = new ApiResponse();

            try
            {
                var projects = await _unitOfWork.Projects.GetAllAsync(
                    filter: null,
                    include: query => query
                        .Include(p => p.ProjectManager)
                        .Include(p => p.Tasks)
                        .Include(p => p.AIAlerts)
                );

                var response = _mapper.Map<List<ProjectResponse>>(projects);

                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
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

                var response = _mapper.Map<ProjectResponse>(project);

                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
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
                        var body = wordDoc.MainDocumentPart.Document.Body;
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

                            else if (text.StartsWith("Ngày thực tế bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày thực tế bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out startDate);

                            else if (text.StartsWith("Ngày kế hoạch bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày kế hoạch bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out baselineStart);

                            else if (text.StartsWith("Ngày kế hoạch kết thúc:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày kế hoạch kết thúc:", "", StringComparison.OrdinalIgnoreCase).Trim(), out baselineEnd);
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
            catch (ArgumentNullException ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return apiResponse.SetBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return apiResponse.SetBadRequest("Đã xảy ra lỗi bất ngờ khi bóc tách file Word: " + ex.Message);
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

                var material = await _unitOfWork.Materials.GetByIdAsync(request.MaterialId);
                if (material == null)
                    return apiResponse.SetNotFound($"Không tìm thấy vật tư (Material) với ID = {request.MaterialId}");

                var existingRequirement = await _unitOfWork.TaskMaterialRequirements
                    .GetAsync(r => r.TaskId == taskId && r.MaterialId == request.MaterialId);

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
                        MaterialId = request.MaterialId,
                        GrossQuantityRequired = request.GrossQuantityRequired
                    };
                    await _unitOfWork.TaskMaterialRequirements.AddAsync(existingRequirement);
                }

                await _unitOfWork.SaveChangeAsync();

                var responseResult = _mapper.Map<TaskMaterialResponse>(existingRequirement);
                return apiResponse.SetOk(responseResult);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
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

                var projectRequirements = await _unitOfWork.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId,
                    include: query => query
                        .Include(r => r.Material)
                        .Include(r => r.TaskItem)
                );

                var response = _mapper.Map<List<TaskMaterialResponse>>(projectRequirements);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> CalculateMRPForProjectAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Dự án không tồn tại.");

                var requirements = await _unitOfWork.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId
                              && r.TaskItem.Status != cpms_Domain.Models.TaskStatus.COMPLETED
                              && r.TaskItem.ActualProgressPct < 100,
                    include: query => query
                        .Include(r => r.Material)
                        .Include(r => r.TaskItem)
                );

                var requirementsList = requirements.ToList();

                if (!requirementsList.Any())
                    return apiResponse.SetOk(new List<MRPCalculationResponse>());

                var grossRequirementsGroup = requirementsList
                    .GroupBy(r => new
                    {
                        r.MaterialId,
                        MaterialName = r.Material != null ? r.Material.MaterialName : "Vật tư không tên",
                        Unit = r.Material != null ? r.Material.Unit : "Cái"
                    })
                    .Select(g => new
                    {
                        g.Key.MaterialId,
                        g.Key.MaterialName,
                        g.Key.Unit,
                        TotalGross = g.Sum(r => r.GrossQuantityRequired),
                        EarliestNeedDate = g.Min(r => r.TaskItem.BaselineStart)
                    }).ToList();

                // LẤY DANH SÁCH ID VẬT TƯ ĐỂ TRUY VẤN TRÚNG ĐÍCH
                var requiredMaterialIds = grossRequirementsGroup.Select(g => g.MaterialId).Distinct().ToList();

                // TỐI ƯU HÓA TRUY VẤN: Chỉ lấy tồn kho của các Vật tư có trong danh sách yêu cầu
                var currentInventories = await _unitOfWork.Inventories.GetAllAsync(
                    filter: i => requiredMaterialIds.Contains(i.MaterialId)
                );
                var inventoriesList = currentInventories.ToList();

                // TỐI ƯU HÓA TRUY VẤN: Chỉ lọc lấy Purchase Orders chứa các vật tư cần tính toán
                var activePurchaseOrders = await _unitOfWork.PurchaseOrders.GetAllAsync(
                    filter: po => (po.Status == PurchaseOrderStatus.PENDING || po.Status == PurchaseOrderStatus.APPROVED)
                               && po.OrderLineItems.Any(line => requiredMaterialIds.Contains(line.MaterialId)),
                    include: query => query.Include(po => po.OrderLineItems)
                );
                var activePoLines = activePurchaseOrders.SelectMany(po => po.OrderLineItems).ToList();

                var mrpResultList = new List<MRPCalculationResponse>();

                foreach (var gross in grossRequirementsGroup)
                {
                    // Lọc dữ liệu trên danh sách đã được thu nhỏ trong bộ nhớ RAM
                    decimal currentStock = inventoriesList
                        .Where(i => i.MaterialId == gross.MaterialId)
                        .Sum(i => i.QuantityOnHand);

                    decimal reserved = inventoriesList
                        .Where(i => i.MaterialId == gross.MaterialId)
                        .Sum(i => i.ReservedQuantity);

                    decimal available = currentStock - reserved;
                    if (available < 0) available = 0;

                    decimal onOrder = activePoLines
                        .Where(line => line.MaterialId == gross.MaterialId)
                        .Sum(line => line.Quantity);

                    decimal netRequired = gross.TotalGross - available - onOrder;
                    if (netRequired < 0) netRequired = 0;

                    mrpResultList.Add(new MRPCalculationResponse
                    {
                        MaterialId = gross.MaterialId,
                        MaterialName = gross.MaterialName,
                        Unit = gross.Unit,
                        TotalGrossRequired = gross.TotalGross,
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
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Lỗi trong quá trình tính toán MRP: " + ex.Message);
            }
        }
        public async Task<ApiResponse> AdjustProjectBudgetAsync(AdjustBudgetRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId);
                if (project == null)
                    return apiResponse.SetNotFound("Không tìm thấy dự án.");

                decimal oldBudget = project.TotalProjectBudget;

                // Cập nhật ngân sách
                project.TotalProjectBudget += request.Amount;

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

                // Mapping sang Response
                var response = _mapper.Map<ProjectBudgetHistoryResponse>(history);

                // Vì History không có Currency nên gán từ Project
                response.Currency = project.Currency;

                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Lỗi: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetBudgetHistoriesByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                // 1. Lấy dữ liệu từ Database
                var histories = await _unitOfWork.ProjectBudgetHistories.GetAllAsync(h => h.ProjectId == projectId);

                var result = _mapper.Map<List<ProjectBudgetHistoryResponse>>(
                    histories.OrderByDescending(h => h.CreatedAt).ToList()
                );

                // 3. Trả về kết quả
                return apiResponse.SetOk(result);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Lỗi lấy lịch sử ngân sách: " + ex.Message);
            }
        }
    }
}