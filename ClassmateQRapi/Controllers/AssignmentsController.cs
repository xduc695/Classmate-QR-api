using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api")]
    public class AssignmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<AppUser> _userManager;

        public AssignmentsController(AppDbContext context, IWebHostEnvironment env, UserManager<AppUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        // ======================
        // TẠO BÀI TẬP (Teacher)
        // ======================
        [HttpPost("classes/{classId}/assignments")]
        public async Task<IActionResult> CreateAssignment(int classId, [FromForm] AssignmentCreateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(classId);
            if (cls == null) return NotFound("Không tìm thấy lớp học.");

            // Kiểm tra quyền: Phải là giáo viên của lớp hoặc Admin
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
                AssignmentFiles = new List<AssignmentFile>()
            };

            if (request.Attachments != null && request.Attachments.Any())
            {
                // Đường dẫn tới thư mục 'assignments' ở gốc dự án
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
            return Ok(new { message = "Giao bài thành công", assignmentId = assignment.Id });
        }

        // ======================
        // LẤY DANH SÁCH BÀI TẬP
        // ======================
        [Authorize]
        [HttpGet("classes/{classId}/assignments")]
        public async Task<IActionResult> GetAssignments(int classId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Kiểm tra quyền truy cập lớp học (giữ nguyên logic của bạn)

            var data = await _context.Assignments
                .Where(a => a.ClassSectionId == classId)
                .Include(a => a.AssignmentFiles) // Load danh sách file
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AssignmentResponse
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    DueDate = a.DueDate,
                    CreatedAt = a.CreatedAt,
                    ClassSectionId = a.ClassSectionId,
                    Files = a.AssignmentFiles.Select(f => new FileDto
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FileUrl = f.FileUrl
                    }).ToList()
                })
                .ToListAsync();

            return Ok(data);
        }

        // ======================
        // CHI TIẾT BÀI TẬP (ĐÃ ĐỒNG BỘ)
        // ======================
        [Authorize]
        [HttpGet("assignments/{id}")]
        public async Task<IActionResult> GetAssignment(int id)
        {
            var a = await _context.Assignments
                .Include(a => a.AssignmentFiles) // Load danh sách file
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound();

            // Kiểm tra quyền (Teacher/Admin hoặc Student đã Enroll)

            return Ok(new AssignmentResponse
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                DueDate = a.DueDate,
                CreatedAt = a.CreatedAt,
                ClassSectionId = a.ClassSectionId,
                Files = a.AssignmentFiles.Select(f => new FileDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileUrl = f.FileUrl
                }).ToList()
            });
        }

        // ======================
        // SỬA BÀI TẬP (ĐÃ ĐỒNG BỘ - HỖ TRỢ THÊM FILE MỚI)
        // ======================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("assignments/{id}")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromForm] AssignmentCreateRequest request)
        {
            var assignment = await _context.Assignments
                .Include(a => a.AssignmentFiles)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cls = await _context.ClassSections.FindAsync(assignment.ClassSectionId);

            if (cls == null || cls.TeacherId != userId)
                return Forbid("Not your class.");

            // Kiểm tra quyền chủ sở hữu (giữ nguyên logic của bạn)

            assignment.Title = request.Title;
            assignment.Content = request.Content;
            assignment.DueDate = request.DueDate;

            // Nếu giáo viên tải lên thêm file mới
            if (request.Attachments != null && request.Attachments.Any())
            {
                // SỬA TẠI ĐÂY: Trỏ vào thư mục 'assignments' ở gốc dự án
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
                        FileUrl = $"/assignments/{fileName}" // Đường dẫn tương đối cho FE
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật bài tập" });
        }

        // ======================
        // XÓA BÀI TẬP
        // ======================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("assignments/{id}")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var a = await _context.Assignments
          .Include(a => a.AssignmentFiles) // Quan trọng để lấy danh sách file
          .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );
            var isAdmin = roles.Contains("Admin");

            var cls = await _context.ClassSections.FindAsync(a.ClassSectionId);
            if (cls == null) return NotFound("Class not found.");
            if (!isAdmin && cls.TeacherId != userId)
                return Forbid("Not your class.");
            // Xóa file vật lý trên ổ cứng trước khi xóa trong DB
            foreach (var file in a.AssignmentFiles)
            {
                var filePath = Path.Combine(_env.ContentRootPath, file.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            _context.Assignments.Remove(a);
            await _context.SaveChangesAsync();

            return Ok("Deleted.");
        }
    }
}
