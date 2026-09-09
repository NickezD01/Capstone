using cpms_Application.Request.Phase;
using cpms_Application.Response;

namespace cpms_Application.Interfaces;

public interface IPhaseService
{
    Task<ApiResponse> CreatePhaseAsync(int projectId, CreatePhaseRequest request);
    Task<ApiResponse> GetPhasesByProjectAsync(int projectId);
    Task<ApiResponse> GetPhaseByIdAsync(int phaseId);
    Task<ApiResponse> UpdatePhaseAsync(int phaseId, UpdatePhaseRequest request);
    Task<ApiResponse> CancelPhaseAsync(int phaseId, PhaseLifecycleRequest request);
}
