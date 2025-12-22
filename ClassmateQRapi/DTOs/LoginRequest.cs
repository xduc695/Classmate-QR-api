namespace ClassmateQRapi.DTOs
{
    public class LoginRequest
    {
        public string UserNameOrEmail { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
