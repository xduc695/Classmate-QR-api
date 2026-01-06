namespace ClassMate.Api.DTOs
{
    public class GradeBucketDto
    {
        public string Range { get; set; } = null!;
        public int Count { get; set; }
    }

    public class StudentProgressDto
    {
        public string StudentId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;

        public int TotalAssignments { get; set; }
        public int SubmittedAssignments { get; set; }
        public double CompletionRate { get; set; } // 0–100 (%)

        public double? AverageScore { get; set; } // 0–10, null nếu chưa có điểm
    }

    public class ClassProgressReportDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public int TotalStudents { get; set; }
        public int TotalAssignments { get; set; }

        public double CompletionRateOverall { get; set; } // 0–100
        public double? ClassAverageScore { get; set; }

        public List<GradeBucketDto> GradeDistribution { get; set; } = new();
        public List<StudentProgressDto> Students { get; set; } = new();
    }
}
