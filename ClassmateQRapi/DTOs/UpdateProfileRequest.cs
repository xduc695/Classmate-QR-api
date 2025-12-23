using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class UpdateProfileRequest
    {
        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        public string UserName { get; set; } = null!;

        [EmailAddress]
        public string? Email { get; set; }

        public IFormFile? Avatar { get; set; }
    }
}
