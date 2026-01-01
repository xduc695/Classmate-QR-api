using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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
        // ➤ STUDENT NỘP BÀI
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpPost("assignments/{assignmentId}/submit")]
        public async Task<IActionResult> Submit(int assignmentId, [FromForm] SubmissionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var existingSub = await _context.Submissions.AnyAsync(s => s.AssignmentId == assignmentId && s.UserId == userId);
            if (existingSub) return BadRequest("Bạn đã nộp bài rồi. Hãy hủy nộp để nộp lại.");

            var submission = new Submission
            {
                AssignmentId = assignmentId,
                UserId = userId!,
                AnswerText = request.AnswerText,
                SubmissionFiles = new List<SubmissionFile>()
            };

            if (request.Files != null && request.Files.Any())
            {
                // Đường dẫn tới thư mục 'Submissions' ở gốc dự án
                var folder = Path.Combine(_env.ContentRootPath, "Submissions");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Files)
                {
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create)) await file.CopyToAsync(stream);

                    submission.SubmissionFiles.Add(new SubmissionFile
                    {
                        FileName = file.FileName,
                        FileUrl = $"/Submissions/{fileName}"
                    });
                }
            }

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();
            return Ok("Nộp bài thành công");
        }

        [Authorize(Roles = "Student")]
        [HttpDelete("submissions/{submissionId}")]
        public async Task<IActionResult> CancelSubmission(int submissionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var submission = await _context.Submissions
                .Include(s => s.SubmissionFiles)
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.UserId == userId);

            if (submission == null) return NotFound("Không tìm thấy bài nộp.");
            if (DateTime.UtcNow > submission.Assignment.DueDate) return BadRequest("Đã quá hạn nộp bài.");

            // Xóa file vật lý để dọn dẹp bộ nhớ
            foreach (var file in submission.SubmissionFiles)
            {
                var fullPath = Path.Combine(_env.ContentRootPath, file.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }

            _context.Submissions.Remove(submission);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã hủy nộp bài thành công." });
        }
        // ==========================
        // ➤ SV XEM LỊCH SỬ NỘP BÀI
        // ==========================
        [Authorize(Roles = "Student")]
        [HttpGet("assignments/{assignmentId}/submissions/me")]
        public async Task<IActionResult> GetMySubmissions(int assignmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var subs = await _context.Submissions
                .Include(s => s.SubmissionFiles) // Phải Include để lấy danh sách nhiều file
                .Where(s => s.AssignmentId == assignmentId && s.UserId == userId)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new {
                    s.Id,
                    s.AnswerText,
                    s.Score,
                    s.Feedback,
                    s.SubmittedAt,
                    // Trả về danh sách file kèm URL để FE có thể hiển thị/tải về
                    Files = s.SubmissionFiles.Select(f => new { f.Id, f.FileName, f.FileUrl }).ToList()
                })
                .ToListAsync();

            return Ok(subs);
        }

        // ==========================
        // ➤ GV XEM TẤT CẢ BÀI NỘP
        // ==========================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("assignments/{assignmentId}/submissions")]
        public async Task<IActionResult> GetClassSubmissions(int assignmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kiểm tra quyền giáo viên: Chỉ giáo viên của lớp đó mới được xem
            var assignment = await _context.Assignments
                .Include(a => a.ClassSection)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            // Nếu không phải Admin thì phải là giáo viên của lớp
            if (!User.IsInRole("Admin") && assignment.ClassSection.TeacherId != userId)
                return Forbid("Bạn không có quyền xem bài nộp của lớp này.");

            var subs = await _context.Submissions
                .Include(s => s.User)
                .Include(s => s.SubmissionFiles)
                .Where(s => s.AssignmentId == assignmentId)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new {
                    s.Id,
                    s.UserId,
                    studentName = s.User.FullName,
                    s.AnswerText,
                    s.Score,
                    s.Feedback,
                    s.SubmittedAt,
                    Files = s.SubmissionFiles.Select(f => new { f.Id, f.FileName, f.FileUrl }).ToList()
                })
                .ToListAsync();

            return Ok(subs);
        }

        // ==========================
        // ➤ GV CHẤM ĐIỂM
        // ==========================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("submissions/{submissionId}/grade")]
        public async Task<IActionResult> Grade(int submissionId, [FromBody] GradeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sub = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.ClassSection)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (sub == null) return NotFound("Không tìm thấy bài nộp.");

            // Bảo mật: Chỉ giáo viên dạy lớp đó mới được chấm điểm
            if (!User.IsInRole("Admin") && sub.Assignment.ClassSection.TeacherId != userId)
                return Forbid("Bạn không phải giáo viên của lớp này.");

            sub.Score = request.Score;
            sub.Feedback = request.Feedback;
            await _context.SaveChangesAsync();

            return Ok("Chấm điểm thành công.");
        }
    }
}
