using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse> RegisterAsync(UserRegisterRequest userRequest);
        Task<ApiResponse> LoginAsync(LoginRequest request);
        Task<ApiResponse> VerifyEmailAsync(int userId, string verificationCode);

    }
}
