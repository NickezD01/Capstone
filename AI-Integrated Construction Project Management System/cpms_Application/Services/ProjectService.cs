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
                var project = _mapper.Map<Project>(request);
                project.Status = ProjectStatus.PLANNING;

                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Project created successfully");
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
                var projects = await _unitOfWork.Projects.GetAllAsync(null);
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
                var project = await _unitOfWork.Projects.GetByIdAsync(id);

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
            try
            {
                if (file == null || file.Length == 0)
                    return apiResponse.SetBadRequest("Vui lòng tải lên một file Word (.docx) hợp lệ.");

                var currentUser = _claimService.GetUserClaim();
                int pmUserId = currentUser.Id;

                var pmAccount = await _unitOfWork.UserAccounts.GetAsync(u => u.Id == pmUserId);
                if (pmAccount == null)
                    return apiResponse.SetNotFound("Tài khoản quản lý dự án (PMUserID) không tồn tại hoặc không hợp lệ.");

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
                    return apiResponse.SetBadRequest("File văn bản sai cấu trúc hoặc trống. Không tìm thấy dòng chứa tiêu đề 'Tên dự án:'.");

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

                var resultResponse = _mapper.Map<ProjectResponse>(project);

                return apiResponse.SetOk(resultResponse);
            }
            catch (ArgumentNullException ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Đã xảy ra lỗi bất ngờ khi bóc tách file Word: " + ex.Message);
            }
        }

        public async Task<ApiResponse> AssignMaterialRequirementToTaskAsync(int taskId, CreateTaskMaterialRequirementRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
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
                    var newRequirement = new TaskMaterialRequirement
                    {
                        TaskId = taskId,
                        MaterialId = request.MaterialId,
                        GrossQuantityRequired = request.GrossQuantityRequired
                    };
                    await _unitOfWork.TaskMaterialRequirements.AddAsync(newRequirement);
                }

                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk("Gán định mức vật tư cho đầu việc thành công.");
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

        // 🛠️ ĐÃ SỬA: Thêm ép kiểu bẻ cache EF Core (Nếu hàm GetAllAsync hỗ trợ AsNoTracking hoặc tùy biến trực tiếp)
        public async Task<ApiResponse> CalculateMRPForProjectAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Dự án không tồn tại.");

                // 🛑 LƯU Ý QUAN TRỌNG: Lọc trực tiếp từ DB. 
                // Nếu hàm GetAllAsync của bạn có viết nhận AsNoTracking() thì hãy bật nó lên.
                // Ở đây, ta lấy danh sách trực tiếp đảm bảo điều kiện loại bỏ Task COMPLETED và tiến độ đã đạt 100%
                var requirements = await _unitOfWork.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId
                              && r.TaskItem.Status != cpms_Domain.Models.TaskStatus.COMPLETED
                              && r.TaskItem.ActualProgressPct < 100,
                    include: query => query
                        .Include(r => r.Material)
                        .Include(r => r.TaskItem)
                );

                // Mẹo nhỏ: Nếu Generic Repository của bạn không dùng AsNoTracking, bạn có thể ép đọc lại dữ liệu mới nhất từ DB bằng cách chuyển thành List ngay.
                var requirementsList = requirements.ToList();

                if (!requirementsList.Any())
                    return apiResponse.SetOk(new List<MRPCalculationResponse>());

                // Nhóm theo từng Material để tính Tổng Nhu Cầu Thô của các task thực sự CHƯA XONG
                var grossRequirementsGroup = requirementsList
                    .GroupBy(r => new { r.MaterialId, r.Material.MaterialName })
                    .Select(g => new
                    {
                        g.Key.MaterialId,
                        g.Key.MaterialName,
                        TotalGross = g.Sum(r => r.GrossQuantityRequired),
                        EarliestNeedDate = g.Min(r => r.TaskItem.BaselineStart)
                    }).ToList();

                var currentInventories = await _unitOfWork.Inventories.GetAllAsync(null);
                var mrpResultList = new List<MRPCalculationResponse>();

                foreach (var gross in grossRequirementsGroup)
                {
                    decimal currentStock = currentInventories
                        .Where(i => i.MaterialId == gross.MaterialId)
                        .Sum(i => i.QuantityOnHand);

                    decimal netRequired = gross.TotalGross - currentStock;
                    if (netRequired < 0) netRequired = 0;

                    mrpResultList.Add(new MRPCalculationResponse
                    {
                        MaterialId = gross.MaterialId,
                        MaterialName = gross.MaterialName,
                        TotalGrossRequired = gross.TotalGross,
                        CurrentInventory = currentStock,
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
    }
}