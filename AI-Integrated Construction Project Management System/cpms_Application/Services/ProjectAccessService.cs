using cpms_Application.Interfaces;
using cpms_Domain;
using cpms_Domain.Models;

namespace cpms_Application.Services
{
    public class ProjectAccessService : IProjectAccessService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClaimService _claimService;

        public ProjectAccessService(IUnitOfWork unitOfWork, IClaimService claimService)
        {
            _unitOfWork = unitOfWork;
            _claimService = claimService;
        }

        public bool IsCurrentUserAdmin() => IsRole(_claimService.GetUserClaim(), Role.ADMIN);

        public bool IsOwningProjectManager(Project project)
        {
            var user = _claimService.GetUserClaim();
            return IsRole(user, Role.PM) && project.PMUserID == user.Id;
        }

        public bool IsAssignedProjectCustomer(Project project)
        {
            var user = _claimService.GetUserClaim();
            return IsRole(user, Role.CUSTOMER) &&
                   project.CustomerUserId.HasValue &&
                   project.CustomerUserId.Value == user.Id;
        }

        public async Task<bool> CanViewProjectAsStaffAsync(Project project)
        {
            var user = _claimService.GetUserClaim();
            if (IsRole(user, Role.ADMIN)) return true;
            if (IsRole(user, Role.PM)) return project.PMUserID == user.Id;
            if (!IsRole(user, Role.WAREHOUSE_MANAGER)) return false;

            var linkedRequest = await _unitOfWork.MaterialRequests.GetAsync(r =>
                r.ProjectId == project.ProjectId && r.WarehouseId.HasValue && r.Warehouse!.ManagerId == user.Id);
            if (linkedRequest != null) return true;

            var linkedOrder = await _unitOfWork.PurchaseOrders.GetAsync(o =>
                o.ProjectId == project.ProjectId && o.Warehouse.ManagerId == user.Id);
            return linkedOrder != null;
        }

        public async Task<bool> CanReadProjectAsync(Project project) =>
            IsAssignedProjectCustomer(project) || await CanViewProjectAsStaffAsync(project);

        private static bool IsRole(ClaimDTO claim, Role role) =>
            string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
