namespace ClassmateQRapi.Entities
{
    public class ClassSection
    {
        public int Id { get; set; }

        // VD: "MOB101.N11"
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public string? Room { get; set; }

        // 🔥 Mã lớp để sinh viên join bằng code (tự sinh, unique)
        public string JoinCode { get; set; } = null!;

        // Khóa ngoại tới Course
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        // Giảng viên phụ trách
        public string TeacherId { get; set; } = null!;
        public AppUser Teacher { get; set; } = null!;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
