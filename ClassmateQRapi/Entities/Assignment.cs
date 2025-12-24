namespace ClassmateQRapi.Entities
{
    public class Assignment
    {
        public int Id { get; set; }

        public int ClassSectionId { get; set; }
        public ClassSection ClassSection { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;

        public ICollection<AssignmentFile> AssignmentFiles { get; set; } = new List<AssignmentFile>();
        public DateTime DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
