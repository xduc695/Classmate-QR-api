// CreateAttendanceSessionRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CreateAttendanceSessionRequest
    {
        [Required]
        public int ClassSectionId { get; set; }

        public int Minutes { get; set; }

        [Required]
        [StringLength(200)]
        public string TeacherLocation { get; set; } = null!; // VD: "Phòng A1.201"
    }

    public class CheckInRequest
    {
        [Required]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string StudentLocation { get; set; } = null!; // VD: "Khu giảng đường chính"
    }
}