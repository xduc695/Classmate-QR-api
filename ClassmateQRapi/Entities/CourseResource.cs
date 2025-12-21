namespace ClassmateQRapi.Entities
{
    public class CourseResource
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public string? FileUrl { get; set; }
        public string? LinkUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
