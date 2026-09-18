using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Phase;
using cpms_Application.Response;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Application.Services;

public sealed class PhaseService : IPhaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IClaimService _claimService;

    public PhaseService(IUnitOfWork unitOfWork, IMapper mapper, IClaimService claimService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _claimService = claimService;
    }

    public async Task<ApiResponse> CreatePhaseAsync(int projectId, CreatePhaseRequest request)
    {
        var response = new ApiResponse();
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null) return response.SetNotFound("Project not found.");

        var ownerResponse = EnsureOwningPm(project);
        if (ownerResponse != null) return ownerResponse;
        if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
            return response.SetConflict("Closed projects cannot accept new phases.");

        var validation = ValidatePlan(project, request.Name, request.Description, request.SequenceOrder,
            request.BaselineStart, request.BaselineEnd);
        if (validation != null) return validation;

        var duplicate = await _unitOfWork.Phases.GetAsync(phase =>
            phase.ProjectId == projectId &&
            phase.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate != null) return response.SetConflict("A phase with this name already exists in the project.");

        var phase = new Phase
        {
            ProjectId = projectId,
            Project = project,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SequenceOrder = request.SequenceOrder,
            BaselineStart = request.BaselineStart,
            BaselineEnd = request.BaselineEnd,
            Status = PhaseStatus.PLANNED,
            CreatedBy = _claimService.GetUserClaim().Id
        };

        await _unitOfWork.Phases.AddAsync(phase);
        await _unitOfWork.SaveChangeAsync();
        return response.SetApiResponse(HttpStatusCode.Created, true, result: _mapper.Map<cpms_Application.Response.Phase.PhaseResponse>(phase));
    }

    public async Task<ApiResponse> GetPhasesByProjectAsync(int projectId)
    {
        var response = new ApiResponse();
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null) return response.SetNotFound("Project not found.");
        if (!await CanReadProjectAsync(project))
            return response.SetApiResponse(HttpStatusCode.Forbidden, false, "You do not have access to this project's phases.");

        var phases = await _unitOfWork.Phases.GetAllAsync(phase => phase.ProjectId == projectId);
        return response.SetOk(phases
            .OrderBy(phase => phase.SequenceOrder)
            .ThenBy(phase => phase.Name)
            .Select(_mapper.Map<cpms_Application.Response.Phase.PhaseResponse>)
            .ToList());
    }

    public async Task<ApiResponse> GetPhaseByIdAsync(int phaseId)
    {
        var response = new ApiResponse();
        var phase = await _unitOfWork.Phases.GetAsync(p => p.PhaseId == phaseId);
        if (phase == null) return response.SetNotFound("Phase not found.");

        var project = await _unitOfWork.Projects.GetByIdAsync(phase.ProjectId);
        if (project == null) return response.SetNotFound("Project not found.");
        if (!await CanReadProjectAsync(project))
            return response.SetApiResponse(HttpStatusCode.Forbidden, false, "You do not have access to this phase.");

        return response.SetOk(_mapper.Map<cpms_Application.Response.Phase.PhaseResponse>(phase));
    }

    public async Task<ApiResponse> UpdatePhaseAsync(int phaseId, UpdatePhaseRequest request)
    {
        var response = new ApiResponse();
        var phase = await _unitOfWork.Phases.GetByIdAsync(phaseId);
        if (phase == null) return response.SetNotFound("Phase not found.");

        var project = await _unitOfWork.Projects.GetByIdAsync(phase.ProjectId);
        if (project == null) return response.SetNotFound("Project not found.");
        var ownerResponse = EnsureOwningPm(project);
        if (ownerResponse != null) return ownerResponse;
        if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
            return response.SetConflict("Closed projects cannot accept phase changes.");
        if (!MatchesRowVersion(phase.RowVersion, request.RowVersion))
            return response.SetConflict("Phase changed. Reload and retry.");

        var validation = ValidatePlan(project, request.Name, request.Description, request.SequenceOrder,
            request.BaselineStart, request.BaselineEnd);
        if (validation != null) return validation;

        var duplicate = await _unitOfWork.Phases.GetAsync(candidate =>
            candidate.ProjectId == phase.ProjectId && candidate.PhaseId != phaseId &&
            candidate.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate != null) return response.SetConflict("A phase with this name already exists in the project.");

        try
        {
            phase.UpdatePlan(request.Name, request.Description, request.SequenceOrder,
                request.BaselineStart, request.BaselineEnd);
            phase.ModifiedBy = _claimService.GetUserClaim().Id;
            phase.ModifiedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangeAsync();
            return response.SetOk(_mapper.Map<cpms_Application.Response.Phase.PhaseResponse>(phase));
        }
        catch (ArgumentException ex)
        {
            return response.SetBadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return response.SetConflict(ex.Message);
        }
    }

    public async Task<ApiResponse> CancelPhaseAsync(int phaseId, PhaseLifecycleRequest request)
    {
        var response = new ApiResponse();
        var phase = await _unitOfWork.Phases.GetByIdAsync(phaseId);
        if (phase == null) return response.SetNotFound("Phase not found.");

        var project = await _unitOfWork.Projects.GetByIdAsync(phase.ProjectId);
        if (project == null) return response.SetNotFound("Project not found.");
        var ownerResponse = EnsureOwningPm(project);
        if (ownerResponse != null) return ownerResponse;
        if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
            return response.SetConflict("Phases in a closed project cannot change state.");
        if (!MatchesRowVersion(phase.RowVersion, request.RowVersion))
            return response.SetConflict("Phase changed. Reload and retry.");

        try
        {
            phase.Cancel();
            phase.ModifiedBy = _claimService.GetUserClaim().Id;
            phase.ModifiedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangeAsync();
            return response.SetOk(new
            {
                phase.PhaseId,
                Status = phase.Status.ToString(),
                RowVersion = Convert.ToBase64String(phase.RowVersion)
            });
        }
        catch (InvalidOperationException ex)
        {
            return response.SetConflict(ex.Message);
        }
    }

    private static ApiResponse? ValidatePlan(Project project, string name, string? description, int sequenceOrder,
        DateTime baselineStart, DateTime baselineEnd)
    {
        var response = new ApiResponse();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return response.SetBadRequest("Phase name is required and must not exceed 200 characters.");
        if (description?.Trim().Length > 2000)
            return response.SetBadRequest("Phase description must not exceed 2000 characters.");
        if (sequenceOrder < 0)
            return response.SetBadRequest("Sequence order cannot be negative.");
        if (baselineEnd < baselineStart)
            return response.SetBadRequest("Phase baseline dates are invalid.");
        if (baselineStart < project.BaselineStart || baselineEnd > project.BaselineEnd)
            return response.SetBadRequest("Phase dates must stay inside the project baseline period.");
        return null;
    }

    private ApiResponse? EnsureOwningPm(Project project)
    {
        var user = _claimService.GetUserClaim();
        if (!string.Equals(user.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) || project.PMUserID != user.Id)
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                "Only the owning project manager may change this phase.");
        return null;
    }

    private async Task<bool> CanReadProjectAsync(Project project)
    {
        var user = _claimService.GetUserClaim();
        if (IsRole(user, Role.ADMIN)) return true;
        if (IsRole(user, Role.PM)) return project.PMUserID == user.Id;
        if (IsRole(user, Role.CUSTOMER))
            return project.CustomerUserId == user.Id;
        if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return false;

        var request = await _unitOfWork.MaterialRequests.GetAsync(materialRequest =>
            materialRequest.ProjectId == project.ProjectId &&
            materialRequest.WarehouseId.HasValue &&
            materialRequest.Warehouse!.ManagerId == user.Id);
        if (request != null) return true;

        var purchaseOrder = await _unitOfWork.PurchaseOrders.GetAsync(order =>
            order.ProjectId == project.ProjectId && order.Warehouse.ManagerId == user.Id);
        return purchaseOrder != null;
    }

    private static bool IsRole(ClaimDTO user, Role role) =>
        string.Equals(user.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesRowVersion(byte[] current, string supplied) =>
        !string.IsNullOrWhiteSpace(supplied) &&
        Convert.ToBase64String(current).Equals(supplied, StringComparison.Ordinal);
}
