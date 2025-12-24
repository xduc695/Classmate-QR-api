using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClassMate.Api.DTOs
{
    public class AssignmentCreateRequest
    {
        [Required]
        public string Title { get; set; } = null!;

        public string Content { get; set; } = "";

        [Required]
        public DateTime DueDate { get; set; }

        public List<IFormFile>? Attachments { get; set; } // Hỗ trợ nhiều file
    }

    public class AssignmentResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public List<FileDto> Files { get; set; } = new List<FileDto>();
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ClassSectionId { get; set; }
    }
    public class FileDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
    }
}
