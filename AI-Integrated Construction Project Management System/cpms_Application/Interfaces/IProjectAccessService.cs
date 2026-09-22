using cpms_Domain.Models;

namespace cpms_Application.Interfaces
{
    public interface IProjectAccessService
    {
        bool IsCurrentUserAdmin();
        bool IsOwningProjectManager(Project project);
        bool IsAssignedProjectCustomer(Project project);
        Task<bool> CanViewProjectAsStaffAsync(Project project);
        Task<bool> CanReadProjectAsync(Project project);
    }
}
