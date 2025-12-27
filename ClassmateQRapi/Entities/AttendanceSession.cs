namespace ClassmateQRapi.Entities
{
    public class AttendanceSession
    {
        public int Id { get; set; }

        public int ClassSectionId { get; set; }
        public ClassSection ClassSection { get; set; } = null!;

        // Mã dùng để encode vào QR (string là đủ: ví dụ GUID)
        public string Code { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
