using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.User;
using cpms_Application.Response;
using cpms_Application.Response.UserAccount;
using System;
using System.Collections.Generic;
using System.Linq;
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
                var userResponse = _mapper.Map<UserProfileResponse>(user);
                return apiResponse.SetOk(userResponse);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }
        public async Task<ApiResponse> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                var user = await _unitOfWork.UserAccounts.GetAsync(x => x.Id == claim.Id);
                _mapper.Map(updateUserRequest, user);

                user.FirstName = user.FirstName;
                user.LastName = user.LastName;
                user.PhoneNumber = user.PhoneNumber;
                user.ImgUrl = user.ImgUrl;

                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk("Update Success");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
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
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetUserIdAsync()
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                var claim = _claim.GetUserClaim();
                if (claim == null)
                {
                    return apiResponse.SetNotFound("User not found");
                }

                return apiResponse.SetOk(new { UserId = claim.Id });
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> UpdateUserRoleProfileAsync(int Id, UpdateUserRoleRequest updateUserRoleRequest)
        {
            ApiResponse apiResponse = new ApiResponse();
            try
            {
                // Tìm customer theo ID
                var customer = await _unitOfWork.UserAccounts.GetAsync(s => s.Id == Id);
                if (customer == null)
                {
                    return apiResponse.SetNotFound("Customer not found!");
                }

                // Cập nhật Role
                _mapper.Map(updateUserRoleRequest, customer);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Role updated successfully!");
            }
            catch (Exception e)
            {
                return apiResponse.SetBadRequest(e.Message);
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
        
    }
}
