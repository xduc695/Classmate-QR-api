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
    }
}
