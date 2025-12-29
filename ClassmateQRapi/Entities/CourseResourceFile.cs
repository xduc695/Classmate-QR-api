namespace ClassmateQRapi.Entities
{
    public class CourseResourceFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public int CourseResourceId { get; set; }
        public CourseResource CourseResource { get; set; } = null!;
    }
}
