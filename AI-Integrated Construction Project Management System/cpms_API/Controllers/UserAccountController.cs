using cpms_Application.Authorization;
using cpms_Application.Interfaces;
using cpms_Application.Request.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserAccountController : ControllerBase
    {
        private readonly IUserAccountService _service;

        public UserAccountController(IUserAccountService service)
        {
            _service = service;
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpPost("Admin/CreateUser")]
        public async Task<IActionResult> AdminCreateUser(AdminCreateUserRequest request)
        {
            var response = await _service.AdminCreateUserAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize]
        [HttpGet("GetUserProfile")]
        public async Task<IActionResult> GetUserProfileAsync()
        {
            var response = await _service.GetUserProfileAsync();
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize]
        [HttpPut("UpdateUserProfile")]
        public async Task<IActionResult> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            var response = await _service.UpdateUserProfileAsync(updateUserRequest);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpGet("GetAllAccountAsync")]
        public async Task<IActionResult> GetAllAccountAsync()
        {
            var response = await _service.GetAllAccountAsync();
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpGet("GetAccount/{userId:long}")]
        public async Task<IActionResult> GetAccountById(long userId)
        {
            var response = await _service.GetAccountByIdAsync(userId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize]
        [HttpGet("GetUserId")]
        public async Task<IActionResult> GetUserId()
        {
            var response = await _service.GetUserIdAsync();
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpPut("UpdateUserRoleProfile/{userId:long}")]
        public async Task<IActionResult> UpdateUserRole(long userId, UpdateUserRoleRequest request)
        {
            var response = await _service.UpdateUserRoleProfileAsync(userId, request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpGet("CountUser")]
        public async Task<IActionResult> CountUser()
        {
            var response = await _service.CountUser();
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [Authorize(Roles = AppRoles.Admin)]
        [HttpPut("{userId:long}/status")]
        public async Task<IActionResult> SetAccountStatus(long userId, [FromQuery] bool isActive)
        {
            var response = await _service.SetAccountStatusAsync(userId, isActive);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }
    }
}
