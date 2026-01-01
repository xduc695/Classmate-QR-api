using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Báo cáo tiến độ của 1 lớp:
        /// - Tỉ lệ hoàn thành
        /// - Điểm trung bình theo lớp
        /// - Phân bố điểm
        /// </summary>
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("progress/class/{classId}")]
        public async Task<IActionResult> GetClassProgress(int classId)
        {
            // 1. Lấy thông tin lớp
            var cls = await _context.ClassSections
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls == null)
                return NotFound(new { message = "Class not found" });

            // 2. Lấy danh sách sinh viên trong lớp (Enrollments)
            var enrollments = await _context.Enrollments
                .Where(e => e.ClassSectionId == classId)
                .Include(e => e.User)
                .ToListAsync();

            int totalStudents = enrollments.Count;

            // 3. Lấy danh sách bài tập trong lớp
            var assignments = await _context.Assignments
                .Where(a => a.ClassSectionId == classId)
                .ToListAsync();

            int totalAssignments = assignments.Count;

            // Nếu chưa có bài tập hoặc chưa có sinh viên, trả báo cáo rỗng cho đẹp
            if (totalStudents == 0 || totalAssignments == 0)
            {
                var emptyReport = new ClassProgressReportDto
                {
                    ClassId = cls.Id,
                    ClassName = cls.Name,
                    TotalStudents = totalStudents,
                    TotalAssignments = totalAssignments,
                    CompletionRateOverall = 0,
                    ClassAverageScore = null,
                    GradeDistribution = new List<GradeBucketDto>
                    {
                        new GradeBucketDto { Range = "0–<4", Count = 0 },
                        new GradeBucketDto { Range = "4–<6.5", Count = 0 },
                        new GradeBucketDto { Range = "6.5–<8.5", Count = 0 },
                        new GradeBucketDto { Range = "8.5–10", Count = 0 },
                    },
                    Students = enrollments.Select(e => new StudentProgressDto
                    {
                        StudentId = e.UserId,
                        UserName = e.User.UserName ?? "",
                        FullName = e.User.FullName,
                        TotalAssignments = totalAssignments,
                        SubmittedAssignments = 0,
                        CompletionRate = 0,
                        AverageScore = null
                    }).ToList()
                };

                return Ok(emptyReport);
            }

            // 4. Lấy tất cả submissions của lớp
            var assignmentIds = assignments.Select(a => a.Id).ToList();

            var submissions = await _context.Submissions
                .Where(s => assignmentIds.Contains(s.AssignmentId))
                .Include(s => s.User)
                .ToListAsync();

            // 5. Xử lý từng sinh viên
            var studentReports = new List<StudentProgressDto>();

            // dùng cho tính tỉ lệ hoàn thành chung
            int totalSubmittedPairs = 0;

            // dùng cho tính điểm TB lớp
            var studentAverageScores = new List<double>();

            foreach (var enroll in enrollments)
            {
                var studentId = enroll.UserId;

                // submissions của sinh viên này
                var studentSubs = submissions
                    .Where(s => s.UserId == studentId)
                    .ToList();

                // Số bài đã nộp (mỗi assignment chỉ tính 1 nếu có ít nhất 1 submission)
                int submittedAssignments = studentSubs
                    .Select(s => s.AssignmentId)
                    .Distinct()
                    .Count();

                // Cộng vào tổng số cặp (student, assignment) đã nộp
                totalSubmittedPairs += submittedAssignments;

                double completionRate = (double)submittedAssignments / totalAssignments * 100.0;

                // Tính điểm trung bình:
                // với mỗi assignment, lấy submission mới nhất có Score != null
                var latestScoresByAssignment = studentSubs
                    .GroupBy(s => s.AssignmentId)
                    .Select(g =>
                        g.OrderByDescending(x => x.SubmittedAt)
                         .FirstOrDefault(x => x.Score.HasValue)?.Score
                    )
                    .Where(score => score.HasValue)
                    .Select(score => score!.Value)
                    .ToList();

                double? avgScore = null;
                if (latestScoresByAssignment.Count > 0)
                {
                    avgScore = latestScoresByAssignment.Average();
                    studentAverageScores.Add(avgScore.Value);
                }

                studentReports.Add(new StudentProgressDto
                {
                    StudentId = studentId,
                    UserName = enroll.User.UserName ?? "",
                    FullName = enroll.User.FullName,
                    TotalAssignments = totalAssignments,
                    SubmittedAssignments = submittedAssignments,
                    CompletionRate = Math.Round(completionRate, 2),
                    AverageScore = avgScore.HasValue
                        ? Math.Round(avgScore.Value, 2)
                        : (double?)null
                });
            }

            // 6. Tính tỉ lệ hoàn thành chung của lớp
            double completionOverall = (double)totalSubmittedPairs
                / (totalStudents * totalAssignments) * 100.0;

            // 7. Điểm trung bình của lớp
            double? classAverageScore = null;
            if (studentAverageScores.Count > 0)
            {
                classAverageScore = Math.Round(studentAverageScores.Average(), 2);
            }

            // 8. Phân bố điểm (dựa trên điểm trung bình từng sinh viên)
            var buckets = new[]
            {
                new { Range = "0–<4",    Min = 0.0,  Max = 4.0 },
                new { Range = "4–<6.5",  Min = 4.0,  Max = 6.5 },
                new { Range = "6.5–<8.5",Min = 6.5, Max = 8.5 },
                new { Range = "8.5–10",  Min = 8.5, Max = 10.00001 }
            };

            var gradeDistribution = buckets.Select(b => new GradeBucketDto
            {
                Range = b.Range,
                Count = studentReports
                    .Where(s => s.AverageScore.HasValue &&
                                s.AverageScore.Value >= b.Min &&
                                s.AverageScore.Value < b.Max)
                    .Count()
            }).ToList();

            // 9. Đóng gói kết quả
            var report = new ClassProgressReportDto
            {
                ClassId = cls.Id,
                ClassName = cls.Name,
                TotalStudents = totalStudents,
                TotalAssignments = totalAssignments,
                CompletionRateOverall = Math.Round(completionOverall, 2),
                ClassAverageScore = classAverageScore,
                GradeDistribution = gradeDistribution,
                Students = studentReports
            };

            return Ok(report);
        }
    }
}
