using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CreateAttendanceSessionRequest
    {
        [Required]
        public int ClassSectionId { get; set; }

        public int Minutes { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class CheckInRequest
    {
        [Required]
        public string Code { get; set; } = null!;

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
