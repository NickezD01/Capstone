using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Response;
using cpms_Application.Response.ProgressReport;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services;

public sealed class ProgressReportService : IProgressReportService
{
    private static readonly TimeSpan MinimumReportingInterval = TimeSpan.FromMinutes(15);
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IClaimService _claimService;

    public ProgressReportService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
    {
        _uow = uow;
        _mapper = mapper;
        _claimService = claimService;
    }

    public async Task<ApiResponse> SubmitReportAsync(SubmitProgressReportRequest request)
    {
        var currentUser = _claimService.GetUserClaim();
        var task = await _uow.TaskItems.GetByIdAsync(request.TaskId);
        if (task == null) return new ApiResponse().SetNotFound("Task not found.");
        var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
        if (project == null) return new ApiResponse().SetNotFound("Project not found.");
        var isOwner = IsRole(currentUser, Role.PM) && project.PMUserID == currentUser.Id;
        if (!isOwner && task.AssignedToUserID != currentUser.Id)
            return Forbidden("Only the project manager or assigned user may submit progress.");
        if (project.Status is ProjectStatus.PAUSED or ProjectStatus.CANCELLED or ProjectStatus.COMPLETED)
            return new ApiResponse().SetConflict("Progress cannot be submitted while the project is paused or closed.");
        if (task.Status is DomainTaskStatus.CANCELLED or DomainTaskStatus.REJECTED or DomainTaskStatus.COMPLETED)
            return new ApiResponse().SetConflict("Progress cannot be submitted for a closed task.");
        if (request.ProgressIncrement <= 0 || task.ActualProgressPct + request.ProgressIncrement > 100)
            return new ApiResponse().SetBadRequest($"Progress must be positive and cannot exceed the remaining {100 - task.ActualProgressPct}%.");

        var cutoff = DateTime.UtcNow.Subtract(MinimumReportingInterval);
        var recent = await _uow.ProgressReports.GetAsync(r => r.TaskId == task.TaskId &&
            r.ReportedByUserId == currentUser.Id && r.ReportDate >= cutoff &&
            r.Status != ProgressReportStatus.REJECTED && r.Status != ProgressReportStatus.REVERSED &&
            r.Status != ProgressReportStatus.CORRECTED);
        if (recent != null)
            return new ApiResponse().SetConflict("A pending progress report already exists inside the reporting interval.");

        var report = _mapper.Map<ProgressReport>(request);
        report.ReportDate = DateTime.UtcNow;
        report.ReportedByUserId = currentUser.Id;
        report.Status = ProgressReportStatus.PENDING;
        await _uow.ProgressReports.AddAsync(report);
        await _uow.SaveChangeAsync();
        return new ApiResponse().SetApiResponse(HttpStatusCode.Created, true, result: new
        {
            report.ReportId,
            Status = report.Status.ToString(),
            Message = "Progress is pending project-manager approval."
        });
    }

    public async Task<ApiResponse> ApproveReportAsync(int reportId, ReviewProgressReportRequest request)
    {
        var aggregate = await LoadAggregateAsync(reportId);
        if (aggregate.Report == null) return new ApiResponse().SetNotFound("Progress report not found.");
        var access = AuthorizeOwningPm(aggregate.Project!);
        if (access != null) return access;
        if (aggregate.Report.Status != ProgressReportStatus.PENDING)
            return new ApiResponse().SetConflict("Only pending progress reports can be approved.");
        if (!MatchesRowVersion(aggregate.Report.RowVersion, request.RowVersion))
            return new ApiResponse().SetConflict("Progress report changed. Reload and retry.");
        if (aggregate.Project!.Status is ProjectStatus.PAUSED or ProjectStatus.CANCELLED or ProjectStatus.COMPLETED)
            return new ApiResponse().SetConflict("Progress cannot be approved while the project is paused or closed.");
        ProgressReport? original = null;
        if (aggregate.Report.OriginalReportId.HasValue)
        {
            original = await _uow.ProgressReports.GetByIdAsync(aggregate.Report.OriginalReportId.Value);
            if (original == null || original.Status != ProgressReportStatus.APPROVED)
                return new ApiResponse().SetConflict("The original report is no longer eligible for correction.");
        }
        var progressAfterApproval = aggregate.Task!.ActualProgressPct - (original?.ProgressIncrement ?? 0) + aggregate.Report.ProgressIncrement;
        if (progressAfterApproval > 100)
            return new ApiResponse().SetConflict("Approval would make task progress exceed 100%.");
        var costAfterApproval = aggregate.Task.ActualCost - (original?.ActualCostIncrement ?? 0) + aggregate.Report.ActualCostIncrement;
        if (costAfterApproval > aggregate.Task.PlannedBudget && !request.AllowCostOverrun)
            return new ApiResponse().SetConflict("Approval would exceed the task budget. Explicit cost-overrun approval is required.");

        await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            if (original != null)
            {
                ReverseImpact(aggregate.Task, original);
                MarkReviewed(original, ProgressReportStatus.CORRECTED, "Superseded by an approved correction.");
            }
            aggregate.Task.ActualProgressPct += aggregate.Report.ProgressIncrement;
            aggregate.Task.ActualCost = costAfterApproval;
            aggregate.Task.Status = aggregate.Task.ActualProgressPct >= 100 ? DomainTaskStatus.COMPLETED : DomainTaskStatus.IN_PROGRESS;
            MarkReviewed(aggregate.Report, ProgressReportStatus.APPROVED, request.ReviewNote);
            RecalculateProject(aggregate.Project!);
            await _uow.SaveChangeAsync();
            await _uow.CommitTransactionAsync();
            return new ApiResponse().SetOk(new
            {
                aggregate.Report.ReportId,
                Status = aggregate.Report.Status.ToString(),
                aggregate.Task.ActualProgressPct,
                aggregate.Task.ActualCost,
                CostOverrun = aggregate.Task.ActualCost > aggregate.Task.PlannedBudget
            });
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> RejectReportAsync(int reportId, ReviewProgressReportRequest request)
    {
        var aggregate = await LoadAggregateAsync(reportId);
        if (aggregate.Report == null) return new ApiResponse().SetNotFound("Progress report not found.");
        var access = AuthorizeOwningPm(aggregate.Project!);
        if (access != null) return access;
        if (aggregate.Report.Status != ProgressReportStatus.PENDING)
            return new ApiResponse().SetConflict("Only pending progress reports can be rejected.");
        if (!MatchesRowVersion(aggregate.Report.RowVersion, request.RowVersion))
            return new ApiResponse().SetConflict("Progress report changed. Reload and retry.");
        MarkReviewed(aggregate.Report, ProgressReportStatus.REJECTED, request.ReviewNote);
        await _uow.SaveChangeAsync();
        return new ApiResponse().SetOk("Progress report rejected without changing task totals.");
    }

    public async Task<ApiResponse> CorrectReportAsync(int reportId, CorrectProgressReportRequest request)
    {
        if (request.ProgressIncrement <= 0 || request.ActualCostIncrement < 0)
            return new ApiResponse().SetBadRequest("Replacement progress must be positive and cost cannot be negative.");
        var aggregate = await LoadAggregateAsync(reportId);
        if (aggregate.Report == null) return new ApiResponse().SetNotFound("Progress report not found.");
        var access = AuthorizeOwningPm(aggregate.Project!);
        if (access != null) return access;
        if (aggregate.Report.Status != ProgressReportStatus.APPROVED)
            return new ApiResponse().SetConflict("Only an approved report can be corrected.");
        if (!MatchesRowVersion(aggregate.Report.RowVersion, request.RowVersion))
            return new ApiResponse().SetConflict("Progress report changed. Reload and retry.");

        await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var replacement = new ProgressReport
            {
                TaskId = aggregate.Task!.TaskId,
                ReportedByUserId = aggregate.Report.ReportedByUserId,
                ReportDate = DateTime.UtcNow,
                ProgressIncrement = request.ProgressIncrement,
                ActualCostIncrement = request.ActualCostIncrement,
                Notes = request.Notes,
                SitePhotoUrl = request.SitePhotoUrl,
                Status = ProgressReportStatus.PENDING,
                OriginalReportId = aggregate.Report.ReportId
            };
            await _uow.ProgressReports.AddAsync(replacement);
            await _uow.SaveChangeAsync();
            await _uow.CommitTransactionAsync();
            return new ApiResponse().SetApiResponse(HttpStatusCode.Created, true, result: new
            {
                OriginalReportId = aggregate.Report.ReportId,
                ReplacementReportId = replacement.ReportId,
                ReplacementStatus = replacement.Status.ToString()
            });
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> ReverseReportAsync(int reportId, ReviewProgressReportRequest request)
    {
        var aggregate = await LoadAggregateAsync(reportId);
        if (aggregate.Report == null) return new ApiResponse().SetNotFound("Progress report not found.");
        var access = AuthorizeOwningPm(aggregate.Project!);
        if (access != null) return access;
        if (aggregate.Report.Status != ProgressReportStatus.APPROVED)
            return new ApiResponse().SetConflict("Only an approved report can be reversed.");
        if (!MatchesRowVersion(aggregate.Report.RowVersion, request.RowVersion))
            return new ApiResponse().SetConflict("Progress report changed. Reload and retry.");

        await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            ReverseImpact(aggregate.Task!, aggregate.Report);
            MarkReviewed(aggregate.Report, ProgressReportStatus.REVERSED, request.ReviewNote);
            RecalculateProject(aggregate.Project!);
            await _uow.SaveChangeAsync();
            await _uow.CommitTransactionAsync();
            return new ApiResponse().SetOk("Progress impact reversed.");
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> GetReportsByTaskIdAsync(int taskId)
    {
        var task = await _uow.TaskItems.GetByIdAsync(taskId);
        if (task == null) return new ApiResponse().SetNotFound("Task not found.");
        var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
        if (project == null) return new ApiResponse().SetNotFound("Project not found.");
        var user = _claimService.GetUserClaim();
        if (!IsRole(user, Role.ADMIN) && !(IsRole(user, Role.PM) && project.PMUserID == user.Id) && task.AssignedToUserID != user.Id)
            return Forbidden("You do not have access to this task's progress reports.");
        var reports = await _uow.ProgressReports.GetAllAsync(r => r.TaskId == taskId,
            query => query.Include(r => r.Reporter).Include(r => r.Task));
        return new ApiResponse().SetOk(_mapper.Map<List<ProgressReportResponse>>(reports.OrderByDescending(r => r.ReportDate)));
    }

    private async Task<(ProgressReport? Report, TaskItem? Task, Project? Project)> LoadAggregateAsync(int reportId)
    {
        var report = await _uow.ProgressReports.GetByIdAsync(reportId);
        if (report == null) return (null, null, null);
        var task = await _uow.TaskItems.GetByIdAsync(report.TaskId);
        if (task == null) return (report, null, null);
        var project = await _uow.Projects.GetAsync(p => p.ProjectId == task.ProjectId,
            query => query.Include(p => p.Tasks));
        return (report, task, project);
    }

    private ApiResponse? AuthorizeOwningPm(Project project)
    {
        var user = _claimService.GetUserClaim();
        return IsRole(user, Role.PM) && project.PMUserID == user.Id
            ? null
            : Forbidden("Only the owning project manager may review progress.");
    }

    private void MarkReviewed(ProgressReport report, ProgressReportStatus status, string? note)
    {
        report.Status = status;
        report.ReviewedByUserId = _claimService.GetUserClaim().Id;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewNote = note;
    }

    private static void ReverseImpact(TaskItem task, ProgressReport report)
    {
        task.ActualProgressPct = Math.Max(0, task.ActualProgressPct - report.ProgressIncrement);
        task.ActualCost = Math.Max(0, task.ActualCost - report.ActualCostIncrement);
        task.Status = task.ActualProgressPct <= 0 ? DomainTaskStatus.PENDING : DomainTaskStatus.IN_PROGRESS;
    }

    private static void RecalculateProject(Project project)
    {
        if (project.Status is ProjectStatus.PAUSED or ProjectStatus.CANCELLED or ProjectStatus.COMPLETED) return;
        if (DateTime.UtcNow > project.BaselineEnd)
            project.Status = ProjectStatus.DELAYED;
        else
            project.Status = ProjectStatus.IN_PROGRESS;
    }

    private static bool MatchesRowVersion(byte[] current, string supplied) =>
        current.Length == 0 || (!string.IsNullOrWhiteSpace(supplied) && Convert.ToBase64String(current).Equals(supplied, StringComparison.Ordinal));
    private static bool IsRole(ClaimDTO claim, Role role) => string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
    private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, message);
}
