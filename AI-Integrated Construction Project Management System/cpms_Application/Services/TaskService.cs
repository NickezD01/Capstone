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
        private readonly IProjectAccessService _projectAccess;

        public TaskService(IUnitOfWork uow, IMapper mapper, IClaimService claimService, IProjectAccessService? projectAccess = null)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
            _projectAccess = projectAccess ?? new ProjectAccessService(uow, claimService);
        }

        public async Task<ApiResponse> CreateTaskAsync(int phaseId, CreateTaskRequest request)
        {
            var response = new ApiResponse();
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // 1. Kiểm tra phase và project có tồn tại
                var phase = await _uow.Phases.GetByIdAsync(phaseId);
                if (phase == null)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetNotFound($"Phase {phaseId} was not found.");
                }

                var project = await _uow.Projects.GetByIdAsync(phase.ProjectId);
                if (project == null)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetNotFound($"Project {phase.ProjectId} was not found.");
                }

                var currentUser = _claimService.GetUserClaim();
                if (!string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || project.PMUserID != currentUser.Id)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only create tasks for a project you manage.");
                }

                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetConflict(message: "Closed projects cannot accept new tasks.");
                }

                if (phase.Status is PhaseStatus.COMPLETED or PhaseStatus.CANCELLED)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetConflict(message: "Closed or cancelled phases cannot accept new tasks.");
                }

                if (request.BaselineEnd < request.BaselineStart)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetBadRequest(message: "Task baseline dates are invalid.");
                }

                if (request.BaselineStart < project.BaselineStart || request.BaselineEnd > project.BaselineEnd)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetBadRequest(message: "Task baseline dates must stay within the project baseline period.");
                }

                if (request.BaselineStart < phase.BaselineStart || request.BaselineEnd > phase.BaselineEnd)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetBadRequest(message: "Task baseline dates must stay within the phase baseline period.");
                }

                if (request.PlannedBudget < 0)
                {
                    await _uow.RollbackTransactionAsync();
                    return response.SetBadRequest(message: "Task planned budget cannot be negative.");
                }

                if (project.TotalProjectBudget > 0)
                {
                    var existingTasks = await _uow.TaskItems.GetAllAsync(t => t.ProjectId == project.ProjectId &&
                        t.Status != DomainTaskStatus.CANCELLED && t.Status != DomainTaskStatus.REJECTED);
                    if (existingTasks.Sum(t => t.PlannedBudget) + request.PlannedBudget > project.TotalProjectBudget)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetConflict(message: "Total planned task budgets cannot exceed the project budget.");
                    }
                }

                var currentAccount = await _uow.UserAccounts.GetByIdAsync(currentUser.Id);

                // 2. Map dữ liệu cơ bản và cấu hình mặc định cho Task mới
                var taskItem = _mapper.Map<TaskItem>(request);
                taskItem.ProjectId = project.ProjectId;
                taskItem.Project = project;
                taskItem.PhaseId = phase.PhaseId;
                taskItem.Phase = phase;
                taskItem.PhaseName = phase.Name;
                taskItem.AssignedToUserID = currentUser.Id;
                if (currentAccount != null) taskItem.AssignedToUser = currentAccount;
                taskItem.ActualCost = 0;
                taskItem.ActualProgressPct = 0;
                taskItem.Status = DomainTaskStatus.PENDING;

                await _uow.TaskItems.AddAsync(taskItem);

                // 🚀 LƯU LẦN 1: Tạo bản ghi TaskItem để DB sinh mã `taskItem.TaskId` (Tự tăng)
                await _uow.SaveChangeAsync();

                // 3. Xử lý lưu định mức vật tư đi kèm đầu việc (Nếu có dữ liệu truyền lên)
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

                        var requirement = new TaskMaterialRequirement
                        {
                            TaskId = taskItem.TaskId,
                            TaskItem = taskItem,
                            VariantId = variant.VariantId,
                            Variant = variant,
                            GrossQuantityRequired = matRequest.GrossQuantityRequired
                        };

                        taskItem.MaterialRequirements.Add(requirement);
                        await _uow.TaskMaterialRequirements.AddAsync(requirement);
                    }

                    await _uow.SaveChangeAsync();
                }

                await _uow.CommitTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.Created, true,
                    result: _mapper.Map<TaskResponse>(taskItem));
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
                if (!await _projectAccess.CanReadProjectAsync(project))
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this project's tasks.");

                var tasks = await _uow.TaskItems.GetAllAsync(
                    filter: t => t.ProjectId == projectId,
                    include: query => query
                        .Include(t => t.AssignedToUser)
                        .Include(t => t.Phase)
                        .Include(t => t.MaterialRequirements)
                            .ThenInclude(mr => mr.Variant)
                                .ThenInclude(v => v.Material)
                );

                var ordered = tasks
                    .OrderBy(t => t.Phase != null ? t.Phase.SequenceOrder : 0)
                    .ThenBy(t => t.Phase != null ? t.Phase.Name : t.PhaseName)
                    .ThenBy(t => t.BaselineStart)
                    .ThenBy(t => t.TaskName);

                var result = _mapper.Map<IEnumerable<TaskResponse>>(ordered);
                return response.SetOk(result);
            }
            catch (Exception)
            {
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve project tasks.");
            }
        }

        public async Task<ApiResponse> GetTaskByIdAsync(int taskId)
        {
            var task = await _uow.TaskItems.GetAsync(t => t.TaskId == taskId,
                query => query.Include(t => t.AssignedToUser)
                    .Include(t => t.Phase)
                    .Include(t => t.MaterialRequirements)
                    .ThenInclude(r => r.Variant)
                    .ThenInclude(v => v.Material));
            if (task == null) return new ApiResponse().SetNotFound("Task not found.");
            var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
            if (project == null) return new ApiResponse().SetNotFound("Project not found.");
            if (!await CanReadProjectAsync(project))
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false,
                    "You do not have access to this task.");
            return new ApiResponse().SetOk(_mapper.Map<TaskResponse>(task));
        }

        public async Task<ApiResponse> GetAssignedTasksAsync()
        {
            var user = _claimService.GetUserClaim();
            var tasks = await _uow.TaskItems.GetAllAsync(
                t => t.AssignedToUserID == user.Id,
                query => query.Include(t => t.AssignedToUser)
                    .Include(t => t.Phase)
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
            if (task.Status is DomainTaskStatus.COMPLETED or DomainTaskStatus.CANCELLED)
                return new ApiResponse().SetConflict("A closed task cannot be edited.");
            if (!MatchesRowVersion(task.RowVersion, request.RowVersion))
                return new ApiResponse().SetConflict("Task changed. Reload and retry.");

            var phase = await _uow.Phases.GetByIdAsync(request.PhaseId);
            if (phase == null) return new ApiResponse().SetNotFound($"Phase {request.PhaseId} was not found.");
            if (phase.ProjectId != task.ProjectId)
                return new ApiResponse().SetBadRequest("Target phase does not belong to this project.");
            if (phase.Status is PhaseStatus.COMPLETED or PhaseStatus.CANCELLED)
                return new ApiResponse().SetConflict("Closed or cancelled phases cannot accept task updates.");

            if (request.BaselineEnd < request.BaselineStart)
                return new ApiResponse().SetBadRequest("Task baseline dates are invalid.");
            if (request.BaselineStart < project.BaselineStart || request.BaselineEnd > project.BaselineEnd)
                return new ApiResponse().SetBadRequest("Task dates must stay inside the project baseline.");
            if (request.BaselineStart < phase.BaselineStart || request.BaselineEnd > phase.BaselineEnd)
                return new ApiResponse().SetBadRequest("Task dates must stay inside the phase baseline.");

            var otherTasks = await _uow.TaskItems.GetAllAsync(t => t.ProjectId == project.ProjectId && t.TaskId != taskId &&
                t.Status != DomainTaskStatus.CANCELLED && t.Status != DomainTaskStatus.REJECTED);
            if (project.TotalProjectBudget > 0 && otherTasks.Sum(t => t.PlannedBudget) + request.PlannedBudget > project.TotalProjectBudget)
                return new ApiResponse().SetConflict("Total planned task budgets cannot exceed the project budget.");
            if (request.PlannedBudget < task.ActualCost)
                return new ApiResponse().SetConflict("Task budget cannot be reduced below its approved actual cost.");

            try
            {
                task.Phase = phase;
                task.UpdatePlan(phase.PhaseId, phase.Name, request.TaskName, user.Id,
                    request.PlannedBudget, request.BaselineStart, request.BaselineEnd);
                await _uow.SaveChangeAsync();
                return new ApiResponse().SetOk(_mapper.Map<TaskResponse>(task));
            }
            catch (ArgumentException ex)
            {
                return new ApiResponse().SetBadRequest(ex.Message);
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
            var normalizedAction = action.Trim().ToLowerInvariant();
            if (normalizedAction is "cancel" or "reject")
            {
                var pendingReport = await _uow.ProgressReports.GetAsync(report =>
                    report.TaskId == taskId && report.Status == ProgressReportStatus.PENDING);
                if (pendingReport != null)
                    return new ApiResponse().SetConflict("Approve or reject pending progress reports before closing this task.");

                var openMaterialRequest = await _uow.MaterialRequests.GetAsync(materialRequest =>
                    materialRequest.TaskId == taskId &&
                    (materialRequest.Status == MaterialRequestStatuses.Pending ||
                     materialRequest.Status == MaterialRequestStatuses.Approved ||
                     materialRequest.Status == MaterialRequestStatuses.PartiallyApproved ||
                     materialRequest.Status == MaterialRequestStatuses.PartiallyIssued));
                if (openMaterialRequest != null)
                    return new ApiResponse().SetConflict("Cancel, reject, release, or finish open material requests before closing this task.");
            }
            try
            {
                switch (normalizedAction)
                {
                    case "cancel": task.Cancel(); break;
                    case "reject": task.Reject(); break;
                    case "reopen": task.Reopen(); break;
                    default: return new ApiResponse().SetBadRequest("Supported task actions are cancel, reject, and reopen.");
                }
                await _uow.SaveChangeAsync();
                return new ApiResponse().SetOk(new
                {
                    task.TaskId,
                    Status = task.Status.ToString(),
                    RowVersion = Convert.ToBase64String(task.RowVersion)
                });
            }
            catch (InvalidOperationException ex)
            {
                return new ApiResponse().SetConflict(ex.Message);
            }
        }

        private static bool MatchesRowVersion(byte[] current, string supplied) =>
            !string.IsNullOrWhiteSpace(supplied) && Convert.ToBase64String(current).Equals(supplied, StringComparison.Ordinal);

        private Task<bool> CanReadProjectAsync(Project project) => _projectAccess.CanViewProjectAsStaffAsync(project);
    }
}
