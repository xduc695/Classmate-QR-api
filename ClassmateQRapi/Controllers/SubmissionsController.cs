using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ClassMate.Api.DTOs;
using System.Text.Json;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api")]
    public class SubmissionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SubmissionsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ==========================
        // ➤ STUDENT LÀM BÀI VÀ NỘP
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpPost("assignments/{assignmentId}/submit")]
        public async Task<IActionResult> Submit(int assignmentId, [FromBody] AIQuizSubmissionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kiểm tra bài tập tồn tại và có phải là AI Generated không
            var assignment = await _context.Assignments
                .Include(a => a.AIQuestions)
                .Include(a => a.ClassSection)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");
            if (!assignment.IsAIGenerated) return BadRequest("Bài tập này không phải dạng trắc nghiệm AI.");

            // Kiểm tra deadline
            if (DateTime.UtcNow > assignment.DueDate)
                return BadRequest(new { message = "Đã quá hạn nộp bài." });

            // Kiểm tra xem đã nộp chưa
            var existingSub = await _context.Submissions
                .AnyAsync(s => s.AssignmentId == assignmentId && s.UserId == userId);

            if (existingSub) return BadRequest(new { message = "Bạn đã nộp bài rồi. Hãy hủy nộp để nộp lại." });

            // Tính điểm tự động
            var aiQuestions = await _context.AIQuestions
                .Where(q => q.AssignmentId == assignmentId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            int totalQuestions = aiQuestions.Count;
            int correctAnswers = 0;

            // Tạo danh sách chi tiết kết quả từng câu
            var questionResults = new List<QuestionResult>();

            for (int i = 0; i < aiQuestions.Count; i++)
            {
                var question = aiQuestions[i];
                var studentAnswer = request.Answers.FirstOrDefault(a => a.QuestionId == question.Id);

                bool isCorrect = studentAnswer != null && studentAnswer.SelectedAnswer == question.CorrectAnswer;

                if (isCorrect) correctAnswers++;

                questionResults.Add(new QuestionResult
                {
                    QuestionId = question.Id,
                    QuestionText = question.Question,
                    StudentAnswer = studentAnswer?.SelectedAnswer ?? -1,
                    CorrectAnswer = question.CorrectAnswer,
                    IsCorrect = isCorrect,
                    Options = new List<string> { question.OptionA, question.OptionB, question.OptionC, question.OptionD }
                });
            }

            // Tính điểm theo thang 10
            double score = totalQuestions > 0 ? Math.Round((correctAnswers * 10.0) / totalQuestions, 1) : 0;

            // Tạo submission
            var submission = new Submission
            {
                AssignmentId = assignmentId,
                UserId = userId!,
                AnswerText = "", // Không cần AnswerText cho AI quiz
                Score = score,
                Feedback = "Đã chấm điểm tự động",
                SubmittedAt = DateTime.UtcNow,
                AIQuizResults = JsonSerializer.Serialize(questionResults) // Lưu chi tiết kết quả
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Nộp bài thành công",
                submissionId = submission.Id,
                score = score,
                correctAnswers = correctAnswers,
                totalQuestions = totalQuestions,
                results = questionResults
            });
        }

        // ==========================
        // ➤ STUDENT HỦY NỘP BÀI
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpDelete("submissions/{submissionId}")]
        public async Task<IActionResult> CancelSubmission(int submissionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.UserId == userId);

            if (submission == null) return NotFound("Không tìm thấy bài nộp.");
            if (DateTime.UtcNow > submission.Assignment.DueDate) 
                return BadRequest(new { message = "Đã quá hạn nộp bài." });

            _context.Submissions.Remove(submission);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã hủy nộp bài thành công." });
        }

        // ==========================
        // ➤ SV XEM LỊCH SỬ NỘP BÀI CỦA MÌNH
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpGet("assignments/{assignmentId}/submissions/me")]
        public async Task<IActionResult> GetMySubmissions(int assignmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var submissions = await _context.Submissions
                .Where(s => s.AssignmentId == assignmentId && s.UserId == userId)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            var result = submissions.Select(s => new
            {
                s.Id,
                s.Score,
                s.Feedback,
                s.SubmittedAt,
                Results = !string.IsNullOrEmpty(s.AIQuizResults) 
                    ? JsonSerializer.Deserialize<List<QuestionResult>>(s.AIQuizResults)
                    : new List<QuestionResult>()
            }).ToList();

            return Ok(result);
        }

        // ==========================
        // ➤ SV XEM CHI TIẾT BÀI NỘP
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpGet("submissions/{submissionId}/details")]
        public async Task<IActionResult> GetSubmissionDetails(int submissionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.AIQuestions)
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.UserId == userId);

            if (submission == null) return NotFound("Không tìm thấy bài nộp.");

            var questionResults = !string.IsNullOrEmpty(submission.AIQuizResults)
                ? JsonSerializer.Deserialize<List<QuestionResult>>(submission.AIQuizResults)
                : new List<QuestionResult>();

            // Lấy thông tin chi tiết các câu hỏi
            var detailedResults = new List<object>();
            var aiQuestions = submission.Assignment.AIQuestions.OrderBy(q => q.OrderIndex).ToList();

            foreach (var result in questionResults)
            {
                var question = aiQuestions.FirstOrDefault(q => q.Id == result.QuestionId);
                if (question != null)
                {
                    detailedResults.Add(new
                    {
                        QuestionId = result.QuestionId,
                        QuestionText = result.QuestionText,
                        Options = result.Options,
                        StudentAnswer = result.StudentAnswer,
                        CorrectAnswer = result.CorrectAnswer,
                        IsCorrect = result.IsCorrect,
                        StudentAnswerText = result.StudentAnswer >= 0 && result.StudentAnswer < 4 
                            ? result.Options[result.StudentAnswer] 
                            : "Không trả lời",
                        CorrectAnswerText = result.CorrectAnswer >= 0 && result.CorrectAnswer < 4 
                            ? result.Options[result.CorrectAnswer] 
                            : "N/A"
                    });
                }
            }

            return Ok(new
            {
                submissionId = submission.Id,
                assignmentId = submission.AssignmentId,
                assignmentTitle = submission.Assignment.Title,
                score = submission.Score,
                submittedAt = submission.SubmittedAt,
                totalQuestions = aiQuestions.Count,
                correctAnswers = questionResults.Count(r => r.IsCorrect),
                detailedResults = detailedResults
            });
        }

        // ==========================
        // ➤ GV XEM TẤT CẢ BÀI NỘP CỦA LỚP (CHỈ XEM, KHÔNG CHẤM)
        // ==========================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("assignments/{assignmentId}/submissions")]
        public async Task<IActionResult> GetClassSubmissions(int assignmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kiểm tra quyền giáo viên
            var assignment = await _context.Assignments
                .Include(a => a.ClassSection)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            if (!User.IsInRole("Admin") && assignment.ClassSection.TeacherId != userId)
                return Forbid("Bạn không có quyền xem bài nộp của lớp này.");

            var submissions = await _context.Submissions
                .Include(s => s.User)
                .Where(s => s.AssignmentId == assignmentId)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    StudentName = s.User.FullName,
                    s.Score,
                    s.SubmittedAt,
                    HasResults = !string.IsNullOrEmpty(s.AIQuizResults)
                })
                .ToListAsync();

            // Tính thống kê
            var stats = new
            {
                totalSubmissions = submissions.Count,
                averageScore = submissions.Count > 0 ? Math.Round(submissions.Average(s => s.Score), 1) : 0,
                maxScore = submissions.Count > 0 ? submissions.Max(s => s.Score) : 0,
                minScore = submissions.Count > 0 ? submissions.Min(s => s.Score) : 0
            };

            return Ok(new
            {
                submissions,
                statistics = stats
            });
        }

        // ==========================
        // ➤ GV XEM CHI TIẾT BÀI NỘP CỦA SINH VIÊN
        // ==========================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("submissions/{submissionId}/review")]
        public async Task<IActionResult> ReviewSubmission(int submissionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var submission = await _context.Submissions
                .Include(s => s.User)
                .Include(s => s.Assignment)
                .ThenInclude(a => a.ClassSection)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound("Không tìm thấy bài nộp.");

            // Kiểm tra quyền
            if (!User.IsInRole("Admin") && submission.Assignment.ClassSection.TeacherId != userId)
                return Forbid("Bạn không có quyền xem bài nộp này.");

            var questionResults = !string.IsNullOrEmpty(submission.AIQuizResults)
                ? JsonSerializer.Deserialize<List<QuestionResult>>(submission.AIQuizResults)
                : new List<QuestionResult>();

            return Ok(new
            {
                submissionId = submission.Id,
                studentId = submission.UserId,
                studentName = submission.User.FullName,
                assignmentId = submission.AssignmentId,
                assignmentTitle = submission.Assignment.Title,
                score = submission.Score,
                submittedAt = submission.SubmittedAt,
                results = questionResults
            });
        }

        // Helper class for question results
        public class QuestionResult
        {
            public int QuestionId { get; set; }
            public string QuestionText { get; set; } = null!;
            public int StudentAnswer { get; set; } // 0=A, 1=B, 2=C, 3=D
            public int CorrectAnswer { get; set; }
            public bool IsCorrect { get; set; }
            public List<string> Options { get; set; } = new List<string>();
        }
    }
}