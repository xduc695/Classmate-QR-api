using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CourseCreateRequest
    {
        [Required]
        public string Name { get; set; } = null!;

        [Required]
        public string Code { get; set; } = null!;

        public string? Description { get; set; }
    }

    public class CourseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string? Description { get; set; }
    }
}
