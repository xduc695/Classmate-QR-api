namespace ClassmateQRapi.Entities
{
    public class Submission
    {
        public int Id { get; set; }

        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; } = null!;

        public string UserId { get; set; } = null!;
        public AppUser User { get; set; } = null!;

        public ICollection<SubmissionFile> SubmissionFiles { get; set; } = new List<SubmissionFile>();
        public string? AnswerText { get; set; } // Nội dung trả lời NLHS

        public double? Score { get; set; }
        public string? Feedback { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
