using System.ComponentModel.DataAnnotations;

namespace ClassmateQRapi.DTOs
{
    public class RegisterRequest
    {
        [Required]
        public string UserName { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;

        // Student / Teacher / Admin...
        public string? Role { get; set; }


        // File avatar (có thể null)
        public IFormFile? Avatar { get; set; }
    }
}
