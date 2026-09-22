using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.User;
using cpms_Application.Response;
using cpms_Application.Response.UserAccount;
using cpms_Application.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class UserAccountService : IUserAccountService
    {
        private IUnitOfWork _unitOfWork;
        private IMapper _mapper;
        private IClaimService _claim;
        public UserAccountService(IUnitOfWork unitOfWork, IMapper mapper, IClaimService claim)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _claim = claim;
        }
        public async Task<ApiResponse> GetUserProfileAsync()
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                var user = await _unitOfWork.UserAccounts.GetAsync(x => x.Id == claim.Id);
                if (user == null) return apiResponse.SetNotFound("User not found.");
                var userResponse = _mapper.Map<UserProfileResponse>(user);
                return apiResponse.SetOk(userResponse);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve the user profile.");
            }
        }
        public async Task<ApiResponse> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                var user = await _unitOfWork.UserAccounts.GetAsync(x => x.Id == claim.Id);
                if (user == null) return apiResponse.SetNotFound("User not found.");
                _mapper.Map(updateUserRequest, user);

                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk("Update Success");
            }
            catch (Exception)
            {
                return InternalError("Unable to update the user profile.");
            }
        }
        public async Task<ApiResponse> GetAllAccountAsync()
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var user = await _unitOfWork.UserAccounts.GetAllAsync(null);
                var userResponse = _mapper.Map<List<AccountResponse>>(user);
                return apiResponse.SetOk(userResponse);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve accounts.");
            }
        }

        public async Task<ApiResponse> CreateAccountAsync(CreateUserAccountRequest request)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                if (request.Password != request.ConfirmPassword ||
                    !PasswordSecurity.IsStrongPassword(request.Password))
                    return apiResponse.SetBadRequest("The password does not satisfy the security policy.");
                if (!Enum.IsDefined(request.Role))
                    return apiResponse.SetBadRequest("Invalid account role.");
                if (request.Role == cpms_Domain.Models.Role.SUPPLIER)
                    return apiResponse.SetBadRequest("Supplier accounts are retired and cannot be assigned.");
                var normalizedEmail = request.Email.Trim().ToUpperInvariant();
                var existing = await _unitOfWork.UserAccounts.GetAsync(x =>
                    x.NormalizedEmail == normalizedEmail || (x.Email != null && x.Email.ToUpper() == normalizedEmail));
                if (existing != null)
                    return apiResponse.SetApiResponse(HttpStatusCode.Conflict, false,
                        "An account with this email already exists.");
                var (hash, salt) = PasswordSecurity.CreateHash(request.Password);
                var user = new cpms_Domain.Models.UserAccount
                {
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Email = request.Email.Trim().ToLowerInvariant(),
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                    Role = request.Role,
                    IsEmailVerified = true,
                    PasswordChangedAt = DateTime.UtcNow
                };
                await _unitOfWork.UserAccounts.AddAsync(user);
                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.Created, true,
                    "Account created and verified.", user.Id);
            }
            catch (Exception)
            {
                return InternalError("Unable to create the account.");
            }
        }
        public async Task<ApiResponse> GetCustomersAsync(string? search)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
                var customers = await _unitOfWork.UserAccounts.GetAllAsync(x =>
                    x.Role == cpms_Domain.Models.Role.CUSTOMER && x.IsEmailVerified == true &&
                    (term == null || ((x.FirstName ?? "") + " " + (x.LastName ?? "") + " " + (x.Email ?? "")).ToLower().Contains(term)));
                var result = customers
                    .OrderBy(x => x.LastName)
                    .ThenBy(x => x.FirstName)
                    .Select(x => new CustomerListResponse
                    {
                        Id = x.Id,
                        FirstName = x.FirstName ?? string.Empty,
                        LastName = x.LastName ?? string.Empty,
                        Email = x.Email ?? string.Empty
                    })
                    .ToList();
                return apiResponse.SetOk(result);
            }
            catch (Exception)
            {
                return InternalError("Unable to retrieve customers.");
            }
        }
        public Task<ApiResponse> GetUserIdAsync()
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                if (claim == null)
                {
                    return Task.FromResult(apiResponse.SetNotFound("User not found"));
                }

                return Task.FromResult(apiResponse.SetOk(new { UserId = claim.Id }));
            }
            catch (Exception)
            {
                return Task.FromResult(InternalError("Unable to retrieve the current user identifier."));
            }
        }

        public async Task<ApiResponse> UpdateUserRoleProfileAsync(int Id, UpdateUserRoleRequest updateUserRoleRequest)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                if (!Enum.IsDefined(updateUserRoleRequest.Role))
                    return apiResponse.SetBadRequest(message: "Invalid account role.");
                if (updateUserRoleRequest.Role == cpms_Domain.Models.Role.SUPPLIER)
                    return apiResponse.SetBadRequest(message: "Supplier accounts are retired and cannot be assigned.");
                // Tìm customer theo ID
                var customer = await _unitOfWork.UserAccounts.GetAsync(s => s.Id == Id);
                if (customer == null)
                {
                    return apiResponse.SetNotFound("Customer not found!");
                }

                if (customer.Role == cpms_Domain.Models.Role.ADMIN && updateUserRoleRequest.Role != cpms_Domain.Models.Role.ADMIN)
                {
                    var admins = await _unitOfWork.UserAccounts.GetAllAsync(x => x.Role == cpms_Domain.Models.Role.ADMIN);
                    if (admins.Count <= 1) return apiResponse.SetConflict(message: "The last administrator cannot be demoted.");
                }
                if (customer.Role == cpms_Domain.Models.Role.WAREHOUSE_MANAGER && updateUserRoleRequest.Role != cpms_Domain.Models.Role.WAREHOUSE_MANAGER &&
                    await _unitOfWork.Warehouses.GetAsync(x => x.ManagerId == customer.Id) != null)
                    return apiResponse.SetConflict(message: "Reassign this user's warehouses before changing their role.");
                if (customer.Role == cpms_Domain.Models.Role.PM && updateUserRoleRequest.Role != cpms_Domain.Models.Role.PM &&
                    await _unitOfWork.Projects.GetAsync(x => x.PMUserID == customer.Id) != null)
                    return apiResponse.SetConflict(message: "Reassign this user's projects before changing their role.");

                // Cập nhật Role
                _mapper.Map(updateUserRoleRequest, customer);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Role updated successfully!");
            }
            catch (Exception)
            {
                return InternalError("Unable to update the user role.");
            }
        }
        public async Task<ApiResponse> CountUser()
        {
            var users = await _unitOfWork.UserAccounts.GetAllAsync(null);
            int count = 0;
            foreach (var user in users)
            {
                count++;
            }
            return new ApiResponse().SetOk(count);
        }

        private static ApiResponse InternalError(string message) =>
            new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, message);
        
    }
}
