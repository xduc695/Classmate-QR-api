using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CreateAttendanceSessionRequest
    {
        [Required]
        public int ClassSectionId { get; set; }

        // Thời gian bắt đầu, kết thúc buổi điểm danh (UTC hoặc local, tự quy ước)
        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }
    }

    public class CheckInRequest
    {
        [Required]
        public string Code { get; set; } = null!;
    }
}
