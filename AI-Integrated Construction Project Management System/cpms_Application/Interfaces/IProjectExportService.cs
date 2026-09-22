using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IProjectExportService
    {
        Task<ApiResponse> ExportProjectAsync(int projectId);
    }
}
