namespace ClassmateQRapi.DTOs
{
    public class AttendanceStatusResponse
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? CheckedInAt { get; set; }
        public string? StudentLocation { get; set; }
        public bool IsPresent { get; set; }
    }
}
