using System;
using System.Collections.Generic;

namespace ClassmateQRapi.Entities
{
    public class Submission
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public string UserId { get; set; } = null!;
        public string? AnswerText { get; set; }
        public double Score { get; set; }
        public string? Feedback { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // 🔥 THÊM TRƯỜNG MỚI: Lưu kết quả chi tiết bài làm AI Quiz
        public string? AIQuizResults { get; set; }

        // Navigation properties
        public Assignment Assignment { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public ICollection<SubmissionFile> SubmissionFiles { get; set; } = new List<SubmissionFile>();
    }
}