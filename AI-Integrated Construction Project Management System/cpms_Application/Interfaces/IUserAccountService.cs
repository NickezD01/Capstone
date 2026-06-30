using cpms_Application.Request.User;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IUserAccountService
    {
        Task<ApiResponse> AdminCreateUserAsync(AdminCreateUserRequest request);
        Task<ApiResponse> GetAccountByIdAsync(long id);
        Task<ApiResponse> GetUserProfileAsync();
        Task<ApiResponse> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest);
        Task<ApiResponse> UpdateUserRoleProfileAsync(long Id, UpdateUserRoleRequest updateUserRoleRequest);
        Task<ApiResponse> GetAllAccountAsync();
        Task<ApiResponse> GetUserIdAsync();
        Task<ApiResponse> CountUser();
        Task<ApiResponse> SetAccountStatusAsync(long id, bool isActive);
        //Task<ApiResponse> GetMemberAccount();
        //Task<ApiResponse> GetMemberByPlanName(SubscriptionPlanName name);
    }
}
