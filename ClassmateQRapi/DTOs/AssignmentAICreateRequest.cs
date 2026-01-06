// ClassMate.Api.DTOs/AssignmentAICreateRequest.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClassMate.Api.DTOs
{
    public class AssignmentAICreateRequest
    {
        [Required]
        public string Title { get; set; } = null!;

        public string Content { get; set; } = "";

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public string Subject { get; set; } = null!;

        [Required]
        public string Difficulty { get; set; } = null!;

        [Required]
        [Range(1, 50)]
        public int NumberOfQuestions { get; set; } = 10;

        public List<IFormFile>? Attachments { get; set; }
    }

    

    public class AIQuestionDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = null!;
        public string OptionA { get; set; } = null!;
        public string OptionB { get; set; } = null!;
        public string OptionC { get; set; } = null!;
        public string OptionD { get; set; } = null!;
        public int CorrectAnswer { get; set; } // 0=A, 1=B, 2=C, 3=D
        public int OrderIndex { get; set; }
    }

    public class AIQuestionUpdateDto
    {
        [Required]
        public string Question { get; set; } = null!;

        [Required]
        public string OptionA { get; set; } = null!;

        [Required]
        public string OptionB { get; set; } = null!;

        [Required]
        public string OptionC { get; set; } = null!;

        [Required]
        public string OptionD { get; set; } = null!;

        [Required]
        [Range(0, 3)]
        public int CorrectAnswer { get; set; }
    }
}