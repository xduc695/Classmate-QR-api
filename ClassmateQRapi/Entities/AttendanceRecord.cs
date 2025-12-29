namespace ClassmateQRapi.Entities
{
    public class AttendanceRecord
    {
        public int Id { get; set; }

        public int AttendanceSessionId { get; set; }
        public AttendanceSession AttendanceSession { get; set; } = null!;

        public string UserId { get; set; } = null!;
        public AppUser User { get; set; } = null!;

        public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
