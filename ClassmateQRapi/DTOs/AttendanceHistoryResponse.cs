namespace ClassmateQRapi.DTOs
{
    public class AttendanceHistoryResponse
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? StudentCode { get; set; }
        public int TotalSessions { get; set; }
        public int AttendedCount { get; set; }
        public double AttendanceRate { get; set; }
        public List<object> RecentSessions { get; set; } = new List<object>();
    }
}
