using System.ComponentModel.DataAnnotations;

namespace ClassmateQRapi.DTOs
{
    public class UpdateUserRoleRequest
    {
        [Required]
        public string Role { get; set; } = null!; // "Student" / "Teacher" / "Admin"
    }
}
