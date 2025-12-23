using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClassMate.Api.DTOs
{
    public class SubmissionRequest
    {
        public string? AnswerText { get; set; }
        public IFormFile? File { get; set; }
    }

    public class GradeRequest
    {
        [Required]
        public double Score { get; set; }
        public string? Feedback { get; set; }
    }
}
