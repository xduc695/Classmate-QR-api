using System.ComponentModel.DataAnnotations;

namespace ClassmateQRapi.DTOs
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập email hoặc tên đăng nhập")]
        public string UserNameOrEmail { get; set; } = null!;
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        public string Password { get; set; } = null!;
    }
}
