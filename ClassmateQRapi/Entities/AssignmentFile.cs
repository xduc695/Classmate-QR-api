namespace ClassmateQRapi.Entities
{
    public class AssignmentFile
    {
        public int Id { get; set; }
        public string FileUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; } = null!;
    }
}
