namespace ClassmateQRapi.Entities
{
    public class Enrollment
    {
        public int Id { get; set; }   // PK đơn

        public string UserId { get; set; } = null!;
        public AppUser User { get; set; } = null!;

        public int ClassSectionId { get; set; }
        public ClassSection ClassSection { get; set; } = null!;

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}
