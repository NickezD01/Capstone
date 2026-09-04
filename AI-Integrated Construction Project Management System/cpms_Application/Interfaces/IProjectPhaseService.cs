using cpms_Application.Request.ProjectPhase;
using cpms_Application.Response;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IProjectPhaseService
    {
        Task<ApiResponse> CreateAsync(CreateProjectPhaseRequest request);
        Task<ApiResponse> GetByIdAsync(int id);
        Task<ApiResponse> GetByProjectIdAsync(int projectId);
        Task<ApiResponse> UpdateAsync(UpdateProjectPhaseRequest request);
        Task<ApiResponse> DeleteAsync(int id);
    }
}
