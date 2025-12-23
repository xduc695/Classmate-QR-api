using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class CourseResourceCreateRequest
    {
        [Required]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public IFormFile? File { get; set; }
        public string? LinkUrl { get; set; }
    }

    public class CourseResourceResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? FileUrl { get; set; }
        public string? LinkUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
