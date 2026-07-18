using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using cpms_Application.Response.Tasks;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Tránh lỗi Ambiguous (trùng tên) giữa Task của hệ thống và Entity TaskStatus của Domain
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public TaskService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateTaskAsync(CreateTaskRequest request)
        {
            var response = new ApiResponse();
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // 1. Kiểm tra xem dự án (Project) có tồn tại thực tế hay không
                // Thay thế GetAsync bằng hàm tìm kiếm theo ID tương ứng trong cấu trúc của bạn (ví dụ: GetByIdAsync hoặc GetAsync)
                var project = await _uow.Projects.GetAsync(p => p.ProjectId == request.ProjectId);
                if (project == null)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetNotFound($"Dự án với ID = {request.ProjectId} không tồn tại trong hệ thống.");
                }
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetConflict(message: "Completed projects cannot accept new tasks.");
                }
                if (request.BaselineStart < project.BaselineStart || request.BaselineEnd > project.BaselineEnd)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetBadRequest(message: "Task baseline dates must stay within the project baseline period.");
                }
                if (project.TotalProjectBudget > 0)
                {
                    var existingTasks = await _uow.TaskItems.GetAllAsync(t => t.ProjectId == request.ProjectId);
                    if (existingTasks.Sum(t => t.PlannedBudget) + request.PlannedBudget > project.TotalProjectBudget)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetConflict(message: "Total planned task budgets cannot exceed the project budget.");
                    }
                }
                var currentUser = _claimService.GetUserClaim();
                if (!string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || project.PMUserID != currentUser.Id)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only create tasks for a project you manage.");
                }

                // 2. Map dữ liệu cơ bản và cấu hình mặc định cho Task mới
                var taskItem = _mapper.Map<TaskItem>(request);
                taskItem.AssignedToUserID = currentUser.Id;
                taskItem.ActualCost = 0;
                taskItem.ActualProgressPct = 0;
                taskItem.Status = DomainTaskStatus.PENDING;

                await _uow.TaskItems.AddAsync(taskItem);

                // 🚀 LƯU LẦN 1: Tạo bản ghi TaskItem để DB sinh mã `taskItem.TaskId` (Tự tăng)
                await _uow.SaveChangeAsync();

                // 4. Xử lý lưu định mức vật tư đi kèm đầu việc (Nếu có dữ liệu truyền lên)
                if (request.Materials != null && request.Materials.Any())
                {
                    if (request.Materials.Any(x => x.GrossQuantityRequired <= 0))
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest("Every material requirement quantity must be greater than zero.");
                    }
                    var requestedVariantKeys = request.Materials.Select(x => x.VariantId > 0 ? $"V:{x.VariantId}" : $"M:{x.MaterialId}");
                    if (requestedVariantKeys.Distinct().Count() != request.Materials.Count)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest("A material variant may only appear once per task.");
                    }
                    // Tối ưu hóa: Thu thập toàn bộ MaterialId cần kiểm tra để truy vấn DB một lần duy nhất
                    var resolvedVariantIds = new HashSet<int>();
                    foreach (var matRequest in request.Materials)
                    {
                        MaterialVariant? variant;
                        if (matRequest.VariantId != 0)
                            variant = await _uow.MaterialVariants.GetByIdAsync(matRequest.VariantId);
                        else
                        {
                            var candidates = await _uow.MaterialVariants.GetAllAsync(v => v.MaterialId == matRequest.MaterialId && v.IsActive);
                            variant = candidates.Count == 1 ? candidates[0] : null;
                        }
                        if (variant == null || !variant.IsActive)
                        {
                            await _uow.RollbackTransactionAsync();
                            return response.SetBadRequest(message: "Material variant does not exist.");
                        }
                        if (!resolvedVariantIds.Add(variant.VariantId))
                        {
                            await _uow.RollbackTransactionAsync();
                            return response.SetBadRequest(message: "A resolved material variant may only appear once per task.");
                        }

                        // Khởi tạo thực thể liên kết Task và Vật tư
                        var requirement = new TaskMaterialRequirement
                        {
                            TaskId = taskItem.TaskId, // Sử dụng mã TaskId vừa sinh tự động ở trên
                            VariantId = variant.VariantId,
                            GrossQuantityRequired = matRequest.GrossQuantityRequired
                        };

                        await _uow.TaskMaterialRequirements.AddAsync(requirement);
                    }

                    // 🚀 LƯU LẦN 2: Lưu toàn bộ danh sách định mức vật tư phụ thuộc vào Database
                    await _uow.SaveChangeAsync();
                }

                await _uow.CommitTransactionAsync();
                return response.SetOk("Khởi tạo đầu việc dự án và định mức vật tư thành công!");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to create the task.");
            }
        }

        public async Task<ApiResponse> GetTasksByProjectAsync(int projectId)
        {
            var response = new ApiResponse();
            try
            {
                var project = await _uow.Projects.GetByIdAsync(projectId);
                if (project == null) return response.SetNotFound("Project not found.");
                if (!await CanReadProjectAsync(project))
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project's tasks.");
                // Tối ưu hóa truy vấn: Lôi kèm User gánh vác, danh sách định mức và thuộc tính của Vật tư để map sang DTO
                var tasks = await _uow.TaskItems.GetAllAsync(
                    filter: t => t.ProjectId == projectId,
                    include: query => query
                        .Include(t => t.AssignedToUser)
                        .Include(t => t.MaterialRequirements)
                            .ThenInclude(mr => mr.Variant)
                                .ThenInclude(v => v.Material)
                );

                var result = _mapper.Map<IEnumerable<TaskResponse>>(tasks);
                return response.SetOk(result);
            }
            catch (Exception)
            {
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve project tasks.");
            }
        }

        public async Task<ApiResponse> GetMaterialRequirementsByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                // 1. Kiểm tra xem dự án (Project) có tồn tại không
                var project = await _uow.Projects.GetAsync(p => p.ProjectId == projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Dự án không tồn tại.");
                if (!await CanReadProjectAsync(project))
                    return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project's material requirements.");

                // 2. 🚀 CẢI TIẾN HIỆU NĂNG: Lọc trực tiếp từ DB bằng Include thay vì bốc toàn bộ bảng định mức lên RAM (In-Memory Filtering)
                // Lấy các định mức vật tư mà có Task thuộc về ProjectId này
                var projectRequirements = await _uow.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId,
                    include: query => query
                        .Include(r => r.Variant)
                            .ThenInclude(v => v.Material)
                        .Include(r => r.TaskItem)
                );

                if (!projectRequirements.Any())
                {
                    return apiResponse.SetOk(new List<TaskMaterialResponse>());
                }

                // 3. Sử dụng AutoMapper để map trực tiếp sang DTO phẳng (Vì đã include đầy đủ dữ liệu cha ở trên)
                var responseData = _mapper.Map<List<TaskMaterialResponse>>(projectRequirements);

                return apiResponse.SetOk(responseData);
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve material requirements.");
            }
        }

        public async Task<ApiResponse> GetAssignedTasksAsync()
        {
            var user = _claimService.GetUserClaim();
            var tasks = await _uow.TaskItems.GetAllAsync(
                t => t.AssignedToUserID == user.Id,
                query => query.Include(t => t.AssignedToUser)
                    .Include(t => t.MaterialRequirements)
                    .ThenInclude(r => r.Variant)
                    .ThenInclude(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<TaskResponse>>(tasks));
        }

        public async Task<ApiResponse> UpdateTaskAsync(int taskId, UpdateTaskRequest request)
        {
            var task = await _uow.TaskItems.GetByIdAsync(taskId);
            if (task == null) return new ApiResponse().SetNotFound("Task not found.");
            var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
            var user = _claimService.GetUserClaim();
            if (project == null || project.PMUserID != user.Id || !string.Equals(user.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase))
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only the owning project manager may update this task.");
            if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                return new ApiResponse().SetConflict("Closed projects cannot accept task changes.");
            if (!MatchesRowVersion(task.RowVersion, request.RowVersion))
                return new ApiResponse().SetConflict("Task changed. Reload and retry.");
            if (request.BaselineStart < project.BaselineStart || request.BaselineEnd > project.BaselineEnd)
                return new ApiResponse().SetBadRequest("Task dates must stay inside the project baseline.");
            var otherTasks = await _uow.TaskItems.GetAllAsync(t => t.ProjectId == project.ProjectId && t.TaskId != taskId);
            if (project.TotalProjectBudget > 0 && otherTasks.Sum(t => t.PlannedBudget) + request.PlannedBudget > project.TotalProjectBudget)
                return new ApiResponse().SetConflict("Total planned task budgets cannot exceed the project budget.");

            try
            {
                task.UpdatePlan(request.PhaseName, request.TaskName, user.Id,
                    request.PlannedBudget, request.BaselineStart, request.BaselineEnd);
                await _uow.SaveChangeAsync();
                return new ApiResponse().SetOk("Task updated.");
            }
            catch (InvalidOperationException ex)
            {
                return new ApiResponse().SetConflict(ex.Message);
            }
        }

        public async Task<ApiResponse> ChangeTaskStatusAsync(int taskId, string action, TaskLifecycleRequest request)
        {
            var task = await _uow.TaskItems.GetByIdAsync(taskId);
            if (task == null) return new ApiResponse().SetNotFound("Task not found.");
            var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
            var user = _claimService.GetUserClaim();
            if (project == null || project.PMUserID != user.Id || !string.Equals(user.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase))
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only the owning project manager may change this task.");
            if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                return new ApiResponse().SetConflict("Tasks in a closed project cannot change state.");
            if (!MatchesRowVersion(task.RowVersion, request.RowVersion))
                return new ApiResponse().SetConflict("Task changed. Reload and retry.");
            try
            {
                switch (action.Trim().ToLowerInvariant())
                {
                    case "cancel": task.Cancel(); break;
                    case "reject": task.Reject(); break;
                    case "reopen": task.Reopen(); break;
                    default: return new ApiResponse().SetBadRequest("Supported task actions are cancel, reject, and reopen.");
                }
                await _uow.SaveChangeAsync();
                return new ApiResponse().SetOk(new { task.TaskId, Status = task.Status.ToString() });
            }
            catch (InvalidOperationException ex)
            {
                return new ApiResponse().SetConflict(ex.Message);
            }
        }

        private static bool MatchesRowVersion(byte[] current, string supplied) =>
            !string.IsNullOrWhiteSpace(supplied) && Convert.ToBase64String(current).Equals(supplied, StringComparison.Ordinal);

        private async Task<bool> CanReadProjectAsync(Project project)
        {
            var user = _claimService.GetUserClaim();
            if (string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(user.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase))
                return project.PMUserID == user.Id;
            if (!string.Equals(user.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase)) return false;

            var request = await _uow.MaterialRequests.GetAsync(r =>
                r.ProjectId == project.ProjectId && r.WarehouseId.HasValue && r.Warehouse!.ManagerId == user.Id);
            if (request != null) return true;
            return await _uow.PurchaseOrders.GetAsync(o =>
                o.ProjectId == project.ProjectId && o.Warehouse.ManagerId == user.Id) != null;
        }
    }
}
