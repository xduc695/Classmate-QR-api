namespace ClassMate.Api.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; } = null!;
        public DateTime Expiration { get; set; }
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
    }
}
