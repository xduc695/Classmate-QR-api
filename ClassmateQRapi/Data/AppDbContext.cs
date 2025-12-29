using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClassmateQRapi.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<ClassSection> ClassSections { get; set; } = null!;
        public DbSet<Enrollment> Enrollments { get; set; } = null!;
        public DbSet<AttendanceSession> AttendanceSessions { get; set; } = null!;
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; } = null!;
        public DbSet<Assignment> Assignments { get; set; } = null!;
        public DbSet<Submission> Submissions { get; set; } = null!;
        public DbSet<CourseResource> CourseResources { get; set; } = null!;
        public DbSet<AssignmentFile> AssignmentFiles { get; set; } = null!;
        public DbSet<SubmissionFile> SubmissionFiles { get; set; } = null!;
        public DbSet<CourseResourceFile> CourseResourceFiles { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Enrollment – như đã sửa trước đó
            builder.Entity<Enrollment>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Enrollment>()
                .HasOne(e => e.ClassSection)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.ClassSectionId)
                .OnDelete(DeleteBehavior.NoAction);

            // ✅ ClassSection -> Course
            builder.Entity<ClassSection>()
                .HasOne(c => c.Course)
                .WithMany(c => c.ClassSections)
                .HasForeignKey(c => c.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ ClassSection -> Teacher
            builder.Entity<ClassSection>()
                .HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // 🔥 JoinCode phải unique
            builder.Entity<ClassSection>()
                .HasIndex(c => c.JoinCode)
                .IsUnique();
            builder.Entity<Submission>()
    .HasOne(s => s.Assignment)
    .WithMany(a => a.Submissions)
    .HasForeignKey(s => s.AssignmentId)
    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Submission>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<CourseResource>()
    .HasOne(r => r.Course)
    .WithMany(c => c.Resources)
    .HasForeignKey(r => r.CourseId)
    .OnDelete(DeleteBehavior.Cascade);
            // Khi xóa Assignment -> Xóa hết AssignmentFiles liên quan
            builder.Entity<AssignmentFile>()
                .HasOne(af => af.Assignment)
                .WithMany(a => a.AssignmentFiles)
                .HasForeignKey(af => af.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Khi xóa Submission -> Xóa hết SubmissionFiles liên quan
            builder.Entity<SubmissionFile>()
                .HasOne(sf => sf.Submission)
                .WithMany(s => s.SubmissionFiles)
                .HasForeignKey(sf => sf.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<CourseResourceFile>()
       .HasOne(rf => rf.CourseResource)
       .WithMany(r => r.ResourceFiles)
       .HasForeignKey(rf => rf.CourseResourceId)
       .OnDelete(DeleteBehavior.Cascade);

        }

    }
}
