using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class ClassSectionCreateRequest
    {
        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }
        public string? Room { get; set; }

        [Required]
        public int CourseId { get; set; }
        // ❌ Không cần JoinCode ở đây – server tự sinh
        [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc không được để trống")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ít nhất một thứ học trong tuần")]
        [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một thứ học")]
        public List<int> StudyDays { get; set; } = new(); // Danh sách các thứ (0-6)

        [Required(ErrorMessage = "Giờ bắt đầu không được để trống")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Giờ kết thúc không được để trống")]
        public TimeSpan EndTime { get; set; }
    }

    public class ClassSectionResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Room { get; set; }

        public int CourseId { get; set; }
        public string CourseName { get; set; } = null!;

        public string TeacherId { get; set; } = null!;
        public string TeacherName { get; set; } = null!;

        // 🔥 Mã lớp share cho sinh viên
        public string JoinCode { get; set; } = null!;
        public bool IsTeacher { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<int> StudyDays { get; set; } = new();
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // 🔥 THÊM StudentCount
        public int StudentCount { get; set; }

        public bool IsActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
        public string ScheduleSummary { get; set; } = null!;
    }
}