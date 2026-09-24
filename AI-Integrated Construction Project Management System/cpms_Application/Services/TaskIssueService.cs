using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using cpms_Application.Response.Tasks;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Application.Services;

public sealed class TaskIssueService : ITaskIssueService
{
    private readonly IUnitOfWork _uow;
    private readonly IClaimService _claimService;
    private readonly IProjectAccessService _projectAccess;

    public TaskIssueService(
        IUnitOfWork uow,
        IClaimService claimService,
        IProjectAccessService? projectAccess = null)
    {
        _uow = uow;
        _claimService = claimService;
        _projectAccess = projectAccess ?? new ProjectAccessService(uow, claimService);
    }

    public async Task<ApiResponse> CreateIssueAsync(int taskId, CreateTaskIssueRequest request)
    {
        var description = (request?.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 2000)
            return new ApiResponse().SetBadRequest("Issue description is required and must not exceed 2000 characters.");
        if (request!.PhotoUrl?.Trim().Length > 1000)
            return new ApiResponse().SetBadRequest("Photo URL must not exceed 1000 characters.");

        var task = await _uow.TaskItems.GetByIdAsync(taskId);
        if (task == null)
            return new ApiResponse().SetNotFound("Task not found.");
        var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
        if (project == null)
            return new ApiResponse().SetNotFound("Project not found.");
        if (!_projectAccess.IsOwningProjectManager(project) && !IsAssignedWorker(task))
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                "Only the owning project manager or the assigned site worker may report a work problem.");

        var user = _claimService.GetUserClaim();
        var issue = new TaskIssue
        {
            TaskId = taskId,
            Task = task,
            ReportedByUserId = user.Id,
            Description = description,
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim(),
            Status = TaskIssueStatus.OPEN,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.TaskIssues.AddAsync(issue);
        await _uow.SaveChangeAsync();
        return new ApiResponse().SetApiResponse(HttpStatusCode.Created, true, result: await MapAsync(issue));
    }

    public async Task<ApiResponse> GetIssuesByTaskAsync(int taskId)
    {
        var task = await _uow.TaskItems.GetByIdAsync(taskId);
        if (task == null)
            return new ApiResponse().SetNotFound("Task not found.");
        var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
        if (project == null)
            return new ApiResponse().SetNotFound("Project not found.");
        var user = _claimService.GetUserClaim();
        var isAdmin = string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !_projectAccess.IsOwningProjectManager(project) && !IsAssignedWorker(task))
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                "You do not have access to this task's issues.");

        var issues = await _uow.TaskIssues.GetAllAsync(i => i.TaskId == taskId);
        var mapped = new List<TaskIssueResponse>();
        foreach (var issue in issues.OrderByDescending(i => i.CreatedAt))
            mapped.Add(await MapAsync(issue));
        return new ApiResponse().SetOk(mapped);
    }

    public async Task<ApiResponse> ResolveIssueAsync(int issueId, ResolveTaskIssueRequest request)
    {
        var issue = await _uow.TaskIssues.GetByIdAsync(issueId);
        if (issue == null)
            return new ApiResponse().SetNotFound("Work problem not found.");
        var task = await _uow.TaskItems.GetByIdAsync(issue.TaskId);
        if (task == null)
            return new ApiResponse().SetNotFound("Task not found.");
        var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
        if (project == null)
            return new ApiResponse().SetNotFound("Project not found.");
        if (!_projectAccess.IsOwningProjectManager(project))
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                "Only the owning project manager may resolve a work problem.");
        if (issue.Status != TaskIssueStatus.OPEN)
            return new ApiResponse().SetConflict("Only open work problems can be resolved.");
        if (!MatchesRowVersion(issue.RowVersion, request?.RowVersion ?? string.Empty))
            return new ApiResponse().SetConflict("Work problem changed. Reload and retry.");
        if (request?.ResolutionNote?.Trim().Length > 2000)
            return new ApiResponse().SetBadRequest("Resolution note must not exceed 2000 characters.");

        issue.Status = TaskIssueStatus.RESOLVED;
        issue.ResolutionNote = string.IsNullOrWhiteSpace(request?.ResolutionNote) ? null : request.ResolutionNote.Trim();
        issue.ResolvedAt = DateTime.UtcNow;
        await _uow.SaveChangeAsync();
        return new ApiResponse().SetOk(await MapAsync(issue));
    }

    private bool IsAssignedWorker(TaskItem task)
    {
        var user = _claimService.GetUserClaim();
        return string.Equals(user.Role, Role.WORKER.ToString(), StringComparison.OrdinalIgnoreCase) &&
            task.AssignedToUserID == user.Id;
    }

    private async Task<TaskIssueResponse> MapAsync(TaskIssue issue)
    {
        var reporter = await _uow.UserAccounts.GetByIdAsync(issue.ReportedByUserId);
        return new TaskIssueResponse
        {
            IssueId = issue.IssueId,
            TaskId = issue.TaskId,
            ReportedByUserId = issue.ReportedByUserId,
            ReportedByName = reporter == null
                ? string.Empty
                : $"{reporter.LastName} {reporter.FirstName}".Trim(),
            Description = issue.Description,
            PhotoUrl = issue.PhotoUrl,
            Status = issue.Status.ToString(),
            ResolutionNote = issue.ResolutionNote,
            CreatedAt = issue.CreatedAt,
            ResolvedAt = issue.ResolvedAt,
            RowVersion = Convert.ToBase64String(issue.RowVersion)
        };
    }

    private static bool MatchesRowVersion(byte[] current, string supplied)
    {
        if (current.Length == 0) return true;
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        try
        {
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                current, Convert.FromBase64String(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
