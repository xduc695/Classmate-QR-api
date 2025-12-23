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

        public IFormFile? Attachment { get; set; }
    }

    public class AssignmentResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ClassSectionId { get; set; }
    }
}
