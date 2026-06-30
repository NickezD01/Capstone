using cpms_Application.Authorization;
using cpms_Application.Interfaces;
using cpms_Application.Request.User;
using cpms_Application.Response;
using cpms_Application.Response.UserAccount;
using cpms_Application.Security;
using cpms_Domain.Models;
using Task = System.Threading.Tasks.Task;

namespace cpms_Application.Services
{
    public class UserAccountService : IUserAccountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClaimService _claim;

        public UserAccountService(IUnitOfWork unitOfWork, IClaimService claim)
        {
            _unitOfWork = unitOfWork;
            _claim = claim;
        }

        public async Task<ApiResponse> AdminCreateUserAsync(AdminCreateUserRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var email = request.Email.Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    return apiResponse.SetBadRequest("Email is required.");
                }

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
                {
                    return apiResponse.SetBadRequest("Password confirmation does not match.");
                }

                if (!AppRoles.IsValid(request.Role))
                {
                    return apiResponse.SetBadRequest("Invalid role.");
                }

                var existingUser = await _unitOfWork.Users.GetAsync(x => x.Email == email);
                if (existingUser != null)
                {
                    return apiResponse.SetBadRequest("The email address is already registered.");
                }

                var user = new User
                {
                    Email = email,
                    FullName = request.FullName.Trim(),
                    Role = AppRoles.Normalize(request.Role),
                    PasswordHash = PasswordHasher.HashPassword(request.Password),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk(ToAccountResponse(user));
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetAccountByIdAsync(long id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var user = await _unitOfWork.Users.GetAsync(x => x.UserId == id);
                if (user == null)
                {
                    return apiResponse.SetNotFound("User not found.");
                }

                return apiResponse.SetOk(ToAccountResponse(user));
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetUserProfileAsync()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                var user = await _unitOfWork.Users.GetAsync(x => x.UserId == claim.Id);
                if (user == null)
                {
                    return apiResponse.SetNotFound("User not found.");
                }

                return apiResponse.SetOk(ToProfileResponse(user));
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                var user = await _unitOfWork.Users.GetAsync(x => x.UserId == claim.Id);
                if (user == null)
                {
                    return apiResponse.SetNotFound("User not found.");
                }

                var requestedName = BuildFullName(updateUserRequest.FirstName, updateUserRequest.LastName);
                if (!string.IsNullOrWhiteSpace(requestedName))
                {
                    user.FullName = requestedName;
                }

                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk(ToProfileResponse(user));
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetAllAccountAsync()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var users = await _unitOfWork.Users.GetAllAsync(null);
                var userResponse = users.Select(ToAccountResponse).ToList();
                return apiResponse.SetOk(userResponse);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public Task<ApiResponse> GetUserIdAsync()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                return Task.FromResult(apiResponse.SetOk(new { UserId = claim.Id }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(apiResponse.SetBadRequest(ex.Message));
            }
        }

        public async Task<ApiResponse> UpdateUserRoleProfileAsync(long id, UpdateUserRoleRequest updateUserRoleRequest)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var user = await _unitOfWork.Users.GetAsync(s => s.UserId == id);
                if (user == null)
                {
                    return apiResponse.SetNotFound("User not found.");
                }

                if (!AppRoles.IsValid(updateUserRoleRequest.Role))
                {
                    return apiResponse.SetBadRequest("Invalid role.");
                }

                user.Role = AppRoles.Normalize(updateUserRoleRequest.Role);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk(ToAccountResponse(user));
            }
            catch (Exception e)
            {
                return apiResponse.SetBadRequest(e.Message);
            }
        }

        public async Task<ApiResponse> CountUser()
        {
            var count = await _unitOfWork.Users.CountAsync();
            return new ApiResponse().SetOk(count);
        }

        public async Task<ApiResponse> SetAccountStatusAsync(long id, bool isActive)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var user = await _unitOfWork.Users.GetAsync(x => x.UserId == id);
                if (user == null)
                {
                    return apiResponse.SetNotFound("User not found.");
                }

                user.IsActive = isActive;
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk(ToAccountResponse(user));
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        private static string BuildFullName(string? firstName, string? lastName)
        {
            return string.Join(" ", new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        }

        private static UserProfileResponse ToProfileResponse(User user)
        {
            return new UserProfileResponse
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                Role = AppRoles.Normalize(user.Role),
                IsActive = user.IsActive
            };
        }

        private static AccountResponse ToAccountResponse(User user)
        {
            return new AccountResponse
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                Role = AppRoles.Normalize(user.Role),
                IsActive = user.IsActive
            };
        }
    }
}
