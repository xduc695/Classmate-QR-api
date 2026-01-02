namespace ClassmateQRapi.DTOs
{
    // Buổi học theo lịch
    public class ClassSessionDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string DayOfWeek { get; set; }
        public string Status { get; set; } // "upcoming", "ongoing", "completed", "cancelled"
        public bool HasAttendanceSession { get; set; }
        public int? AttendanceSessionId { get; set; }
        public string AttendanceSessionCode { get; set; }
    }
}
