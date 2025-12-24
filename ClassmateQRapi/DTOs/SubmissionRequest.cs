using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClassMate.Api.DTOs
{
    public class SubmissionRequest
    {
        public string? AnswerText { get; set; }
        public List<IFormFile>? Files { get; set; } // Hỗ trợ nhiều file
    }

    public class GradeRequest
    {
        [Required]
        public double Score { get; set; }
        public string? Feedback { get; set; }
    }
}
