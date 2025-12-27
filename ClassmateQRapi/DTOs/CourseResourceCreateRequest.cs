using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CourseResourceCreateRequest
    {
        [Required]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public List<IFormFile>? Files { get; set; } // Nhiều file
        public string? LinkUrl { get; set; }
    }

    public class CourseResourceResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public List<FileDto> Files { get; set; } = new();
        public string? LinkUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
