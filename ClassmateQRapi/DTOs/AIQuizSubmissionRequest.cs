using System.ComponentModel.DataAnnotations;

namespace ClassMate.Api.DTOs
{
    public class AIQuizSubmissionRequest
    {
        [Required]
        public List<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
    }

    public class StudentAnswer
    {
        [Required]
        public int QuestionId { get; set; }

        [Required]
        [Range(0, 3, ErrorMessage = "Câu trả lời phải từ 0-3 (0=A, 1=B, 2=C, 3=D)")]
        public int SelectedAnswer { get; set; } // 0=A, 1=B, 2=C, 3=D
    }
}