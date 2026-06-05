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
            return resposne.IsSuccess ? Ok(resposne) : BadRequest(resposne);
        }
        [Authorize]
        [HttpPut("UpdateUserProfile")]
        public async Task<IActionResult> UpdateUserProfileAsync(UpdateUserRequest updateUserRequest)
        {
            var result = await _service.UpdateUserProfileAsync(updateUserRequest);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }
        [Authorize(Roles = "Manager")]
        [HttpGet("GetAllAccountAsync")]
        public async Task<IActionResult> GetAllAccountAsync()
        {
            var resposne = await _service.GetAllAccountAsync();
            return resposne.IsSuccess ? Ok(resposne) : BadRequest(resposne);
        }
        [Authorize]
        [HttpGet("GetUserId")]
        public async Task<IActionResult> GetUserId()
        {
            var result = await _service.GetUserIdAsync();
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }
        [Authorize(Roles = "Manager")]
        [HttpPut("UpdateUserRoleProfile/{customerId}")]
        public async Task<IActionResult> UpdateUserRole(int customerId, UpdateUserRoleRequest request)
        {
            var resposne = await _service.UpdateUserRoleProfileAsync(customerId, request);
            return resposne.IsSuccess ? Ok(resposne) : BadRequest(resposne);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("CountUser")]
        public async Task<IActionResult> CountUser()
        {
            var resposne = await _service.CountUser();
            return resposne.IsSuccess ? Ok(resposne) : BadRequest(resposne);
        }
        
    }
}
