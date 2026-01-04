// ClassmateQRapi.Controllers.AssignmentsController.cs (sửa đầy đủ)
using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using ClassmateQRapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api")]
    public class AssignmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAIService _aiService;

        public AssignmentsController(
            AppDbContext context,
            IWebHostEnvironment env,
            UserManager<AppUser> userManager,
            IAIService aiService)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _aiService = aiService;
        }

        // =========================================
        // TẠO BÀI TẬP THÔNG THƯỜNG (Giữ nguyên)
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost("classes/{classId}/assignments")]
        public async Task<IActionResult> CreateAssignment(int classId, [FromForm] AssignmentCreateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(classId);
            if (cls == null) return NotFound("Không tìm thấy lớp học.");

            // Kiểm tra quyền
            var user = await _userManager.FindByIdAsync(userId!);
            var roles = await _userManager.GetRolesAsync(user!);
            if (!roles.Contains("Admin") && cls.TeacherId != userId)
                return Forbid("Bạn không có quyền giao bài cho lớp này.");

            var assignment = new Assignment
            {
                Title = request.Title,
                Content = request.Content,
                DueDate = request.DueDate,
                ClassSectionId = classId,
                IsAIGenerated = false,
                AssignmentFiles = new List<AssignmentFile>(),
                AIQuestions = new List<AIQuestion>()
            };

            // Xử lý file đính kèm
            if (request.Attachments != null && request.Attachments.Any())
            {
                var folder = Path.Combine(_env.ContentRootPath, "assignments");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Attachments)
                {
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    assignment.AssignmentFiles.Add(new AssignmentFile
                    {
                        FileName = file.FileName,
                        FileUrl = $"/assignments/{fileName}"
                    });
                }
            }

            _context.Assignments.Add(assignment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Giao bài thành công",
                assignmentId = assignment.Id,
                isAIGenerated = false
            });
        }

        // =========================================
        // TẠO BÀI TẬP TỪ AI
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost("classes/{classId}/assignments/ai")]
        public async Task<IActionResult> CreateAssignmentFromAI(int classId, [FromForm] AssignmentAICreateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(classId);
            if (cls == null) return NotFound("Không tìm thấy lớp học.");

            // Kiểm tra quyền
            var user = await _userManager.FindByIdAsync(userId!);
            var roles = await _userManager.GetRolesAsync(user!);
            if (!roles.Contains("Admin") && cls.TeacherId != userId)
                return Forbid("Bạn không có quyền giao bài cho lớp này.");

            try
            {
                // Gọi AI service để generate câu hỏi
                var aiQuestions = await _aiService.GenerateQuestionsAsync(
                    request.Subject,
                    request.Difficulty,
                    request.NumberOfQuestions
                );

                if (!aiQuestions.Any())
                {
                    return BadRequest(new { message = "Không thể tạo câu hỏi từ AI. Vui lòng thử lại." });
                }

                // Tạo assignment
                var assignment = new Assignment
                {
                    Title = request.Title,
                    Content = request.Content,
                    DueDate = request.DueDate,
                    ClassSectionId = classId,
                    IsAIGenerated = true,
                    AISubject = request.Subject,
                    AIDifficulty = request.Difficulty,
                    AssignmentFiles = new List<AssignmentFile>(),
                    AIQuestions = new List<AIQuestion>()
                };

                // Thêm câu hỏi từ AI
                int orderIndex = 0;
                foreach (var aiQuestion in aiQuestions)
                {
                    assignment.AIQuestions.Add(new AIQuestion
                    {
                        Question = aiQuestion.Question,
                        OptionA = aiQuestion.A,
                        OptionB = aiQuestion.B,
                        OptionC = aiQuestion.C,
                        OptionD = aiQuestion.D,
                        CorrectAnswer = aiQuestion.CorrectAnswer,
                        OrderIndex = orderIndex++
                    });
                }

                // Xử lý file đính kèm nếu có
                if (request.Attachments != null && request.Attachments.Any())
                {
                    var folder = Path.Combine(_env.ContentRootPath, "assignments");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    foreach (var file in request.Attachments)
                    {
                        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                        var filePath = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        assignment.AssignmentFiles.Add(new AssignmentFile
                        {
                            FileName = file.FileName,
                            FileUrl = $"/assignments/{fileName}"
                        });
                    }
                }

                _context.Assignments.Add(assignment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Tạo bài tập từ AI thành công",
                    assignmentId = assignment.Id,
                    isAIGenerated = true,
                    totalQuestions = aiQuestions.Count,
                    subject = request.Subject,
                    difficulty = request.Difficulty
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi khi tạo bài tập từ AI: {ex.Message}" });
            }
        }

        // =========================================
        // LẤY DANH SÁCH BÀI TẬP (CẬP NHẬT)
        // =========================================
        [Authorize]
        [HttpGet("classes/{classId}/assignments")]
        public async Task<IActionResult> GetAssignments(int classId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kiểm tra xem user có trong lớp không
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.ClassSectionId == classId && e.UserId == userId);

            var cls = await _context.ClassSections.FindAsync(classId);
            var isTeacher = cls?.TeacherId == userId;

            var user = await _userManager.FindByIdAsync(userId!);
            var roles = await _userManager.GetRolesAsync(user!);
            var isAdmin = roles.Contains("Admin");

            if (!isEnrolled && !isTeacher && !isAdmin)
                return Forbid("Bạn không có quyền xem bài tập của lớp này.");

            var assignments = await _context.Assignments
                .Where(a => a.ClassSectionId == classId)
                .Include(a => a.AssignmentFiles)
                .Include(a => a.AIQuestions)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AssignmentResponse
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    DueDate = a.DueDate,
                    CreatedAt = a.CreatedAt,
                    ClassSectionId = a.ClassSectionId,
                    IsAIGenerated = a.IsAIGenerated,
                    Files = a.AssignmentFiles.Select(f => new FileDto
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FileUrl = f.FileUrl
                    }).ToList(),
                    Questions = a.AIQuestions
                        .OrderBy(q => q.OrderIndex)
                        .Select(q => new AIQuestionDto
                        {
                            Id = q.Id,
                            Question = q.Question,
                            OptionA = q.OptionA,
                            OptionB = q.OptionB,
                            OptionC = q.OptionC,
                            OptionD = q.OptionD,
                            CorrectAnswer = q.CorrectAnswer,
                            OrderIndex = q.OrderIndex
                        }).ToList()
                })
                .ToListAsync();

            return Ok(assignments);
        }

        // =========================================
        // CHI TIẾT BÀI TẬP (CẬP NHẬT)
        // =========================================
        [Authorize]
        [HttpGet("assignments/{id}")]
        public async Task<IActionResult> GetAssignment(int id)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AssignmentFiles)
                .Include(a => a.AIQuestions)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            // Kiểm tra quyền truy cập
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.ClassSectionId == assignment.ClassSectionId && e.UserId == userId);

            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);
            var isTeacher = cls?.TeacherId == userId;

            var user = await _userManager.FindByIdAsync(userId!);
            var roles = await _userManager.GetRolesAsync(user!);
            var isAdmin = roles.Contains("Admin");

            if (!isEnrolled && !isTeacher && !isAdmin)
                return Forbid("Bạn không có quyền xem bài tập này.");

            return Ok(new AssignmentResponse
            {
                Id = assignment.Id,
                Title = assignment.Title,
                Content = assignment.Content,
                DueDate = assignment.DueDate,
                CreatedAt = assignment.CreatedAt,
                ClassSectionId = assignment.ClassSectionId,
                IsAIGenerated = assignment.IsAIGenerated,
                Files = assignment.AssignmentFiles.Select(f => new FileDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileUrl = f.FileUrl
                }).ToList(),
                Questions = assignment.AIQuestions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new AIQuestionDto
                    {
                        Id = q.Id,
                        Question = q.Question,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectAnswer = q.CorrectAnswer,
                        OrderIndex = q.OrderIndex
                    }).ToList()
            });
        }

        // =========================================
        // CẬP NHẬT CÂU HỎI AI
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("assignments/{assignmentId}/questions/{questionId}")]
        public async Task<IActionResult> UpdateAIQuestion(int assignmentId, int questionId, [FromBody] AIQuestionUpdateDto request)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AIQuestions)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            // Kiểm tra quyền
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);
            if (cls == null || cls.TeacherId != userId)
                return Forbid("Bạn không có quyền chỉnh sửa câu hỏi này.");

            var question = assignment.AIQuestions.FirstOrDefault(q => q.Id == questionId);
            if (question == null) return NotFound("Không tìm thấy câu hỏi.");

            // Cập nhật câu hỏi
            question.Question = request.Question;
            question.OptionA = request.OptionA;
            question.OptionB = request.OptionB;
            question.OptionC = request.OptionC;
            question.OptionD = request.OptionD;
            question.CorrectAnswer = request.CorrectAnswer;
            question.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật câu hỏi thành công" });
        }

        // =========================================
        // XÓA CÂU HỎI AI
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("assignments/{assignmentId}/questions/{questionId}")]
        public async Task<IActionResult> DeleteAIQuestion(int assignmentId, int questionId)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AIQuestions)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            // Kiểm tra quyền
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);
            if (cls == null || cls.TeacherId != userId)
                return Forbid("Bạn không có quyền xóa câu hỏi này.");

            var question = assignment.AIQuestions.FirstOrDefault(q => q.Id == questionId);
            if (question == null) return NotFound("Không tìm thấy câu hỏi.");

            // Xóa câu hỏi
            _context.AIQuestions.Remove(question);

            // Cập nhật order index của các câu hỏi còn lại
            var remainingQuestions = assignment.AIQuestions
                .Where(q => q.Id != questionId)
                .OrderBy(q => q.OrderIndex)
                .ToList();

            for (int i = 0; i < remainingQuestions.Count; i++)
            {
                remainingQuestions[i].OrderIndex = i;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa câu hỏi thành công" });
        }

        // =========================================
        // THÊM CÂU HỎI MỚI VÀO BÀI TẬP AI
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost("assignments/{assignmentId}/questions")]
        public async Task<IActionResult> AddAIQuestion(int assignmentId, [FromBody] AIQuestionUpdateDto request)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AIQuestions)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound("Không tìm thấy bài tập.");

            // Kiểm tra quyền
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);
            if (cls == null || cls.TeacherId != userId)
                return Forbid("Bạn không có quyền thêm câu hỏi vào bài tập này.");

            // Tìm order index tiếp theo
            var nextOrderIndex = assignment.AIQuestions.Any()
                ? assignment.AIQuestions.Max(q => q.OrderIndex) + 1
                : 0;

            var newQuestion = new AIQuestion
            {
                Question = request.Question,
                OptionA = request.OptionA,
                OptionB = request.OptionB,
                OptionC = request.OptionC,
                OptionD = request.OptionD,
                CorrectAnswer = request.CorrectAnswer,
                OrderIndex = nextOrderIndex,
                AssignmentId = assignmentId
            };

            _context.AIQuestions.Add(newQuestion);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Thêm câu hỏi thành công",
                questionId = newQuestion.Id
            });
        }

        // =========================================
        // SỬA BÀI TẬP (GIỮ NGUYÊN, CÓ THỂ BỎ QUA)
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("assignments/{id}")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromForm] AssignmentCreateRequest request)
        {
            // Giữ nguyên code cũ
            var assignment = await _context.Assignments
                .Include(a => a.AssignmentFiles)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);

            if (cls == null || cls.TeacherId != userId)
                return Forbid("Không có quyền chỉnh sửa bài tập này.");

            assignment.Title = request.Title;
            assignment.Content = request.Content;
            assignment.DueDate = request.DueDate;

            if (request.Attachments != null && request.Attachments.Any())
            {
                var folder = Path.Combine(_env.ContentRootPath, "assignments");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Attachments)
                {
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    assignment.AssignmentFiles.Add(new AssignmentFile
                    {
                        FileName = file.FileName,
                        FileUrl = $"/assignments/{fileName}"
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật bài tập" });
        }

        // =========================================
        // XÓA BÀI TẬP (GIỮ NGUYÊN)
        // =========================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("assignments/{id}")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AssignmentFiles)
                .Include(a => a.AIQuestions)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );
            var isAdmin = roles.Contains("Admin");

            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);
            if (cls == null) return NotFound("Không tìm thấy lớp học.");
            if (!isAdmin && cls.TeacherId != userId)
                return Forbid("Không có quyền xóa bài tập này.");

            // Xóa file vật lý
            foreach (var file in assignment.AssignmentFiles)
            {
                var filePath = Path.Combine(_env.ContentRootPath, file.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa bài tập thành công" });
        }
    }
}