using System.ComponentModel.DataAnnotations;

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

        // Ngày bắt đầu và kết thúc
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Thứ học trong tuần (lưu dạng bitmask)
        // 0: Chủ nhật, 1: Thứ 2, 2: Thứ 3, ..., 6: Thứ 7
        // Ví dụ: học thứ 2,4,6 => StudyDays = 1 << 1 | 1 << 3 | 1 << 5 = 42
        public int StudyDays { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

        [Required(ErrorMessage = "Giờ bắt đầu không được để trống")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Giờ kết thúc không được để trống")]
        public TimeSpan EndTime { get; set; }
    }
}
