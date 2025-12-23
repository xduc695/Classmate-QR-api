using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class EnrollRequest
    {
        [Required]
        public string ClassCode { get; set; } = null!;  // 🔥 dùng JoinCode
    }
}
