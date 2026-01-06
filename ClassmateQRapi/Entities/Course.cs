namespace ClassmateQRapi.Entities
{
    public class Course
    {
        public int Id { get; set; }

        // VD: "Lập trình Mobile"
        public string Name { get; set; } = null!;

        // VD: "MOB101"
        public string Code { get; set; } = null!;

        public string? Description { get; set; }

        public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
        public ICollection<CourseResource> Resources { get; set; } = new List<CourseResource>();

    }
}
