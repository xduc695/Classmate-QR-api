namespace ClassmateQRapi.Entities
{
    public class SubmissionFile
    {
        public int Id { get; set; }
        public string FileUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public int SubmissionId { get; set; }
        public Submission Submission { get; set; } = null!;
    }
}
