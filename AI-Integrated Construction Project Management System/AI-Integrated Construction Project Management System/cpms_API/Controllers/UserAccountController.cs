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
        public IUserAccountService _service;
        public UserAccountController(IUserAccountService service)
        {
            _service = service;
        }
        [Authorize]
        [HttpGet("GetUserProfile")]
        public async Task<IActionResult> GetUserProfileAsync()
        {
            var resposne = await _service.GetUserProfileAsync();
            return StatusCode((int)resposne.StatusCode, resposne);
        }
        [Authorize]
        [HttpPut("UpdateUserProfile")]
        public async Task<IActionResult> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            var result = await _service.UpdateUserProfileAsync(updateUserRequest);
            return StatusCode((int)result.StatusCode, result);
        }
        [Authorize(Roles = "ADMIN")]
        [HttpGet("GetAllAccountAsync")]
        public async Task<IActionResult> GetAllAccountAsync()
        {
            var resposne = await _service.GetAllAccountAsync();
            return StatusCode((int)resposne.StatusCode, resposne);
        }
        [Authorize]
        [HttpGet("GetUserId")]
        public async Task<IActionResult> GetUserId()
        {
            var result = await _service.GetUserIdAsync();
            return StatusCode((int)result.StatusCode, result);
        }
        [Authorize(Roles = "ADMIN")]
        [HttpPut("UpdateUserRoleProfile/{customerId}")]
        public async Task<IActionResult> UpdateUserRole(int customerId, UpdateUserRoleRequest request)
        {
            var resposne = await _service.UpdateUserRoleProfileAsync(customerId, request);
            return StatusCode((int)resposne.StatusCode, resposne);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet("CountUser")]
        public async Task<IActionResult> CountUser()
        {
            var resposne = await _service.CountUser();
            return StatusCode((int)resposne.StatusCode, resposne);
        }
        
    }
}
