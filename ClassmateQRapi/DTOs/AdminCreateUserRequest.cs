using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class AdminCreateUserRequest
    {
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = "Student";
        public IFormFile? Avatar { get; set; }
    }
}