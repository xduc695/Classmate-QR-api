namespace ClassmateQRapi.DTOs
{
    public class UserInfoResponse
    {
        public string Id { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
