using cpms_Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class ClaimService : IClaimService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClaimService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ClaimDTO GetUserClaim()
        {
            var tokenUserId =
                _httpContextAccessor.HttpContext?.User.FindFirst("UserId");

            var tokenUserRole =
                _httpContextAccessor.HttpContext?.User.FindFirst("Role");

            var tokenUserName =
                _httpContextAccessor.HttpContext?.User.FindFirst("FullName");

            if (tokenUserId == null)
            {
                throw new ArgumentNullException("UserId cannot be found!");
            }

            int userId = int.Parse(tokenUserId.Value);

            string role = tokenUserRole?.Value ?? string.Empty;

            string fullName = tokenUserName?.Value ?? string.Empty;

            var userClaim = new ClaimDTO
            {
                Id = userId,
                Role = role,
                Name = fullName
            };

            return userClaim;
        }
    }

    public class ClaimDTO
    {
        public int Id { get; set; }

        public string Role { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
