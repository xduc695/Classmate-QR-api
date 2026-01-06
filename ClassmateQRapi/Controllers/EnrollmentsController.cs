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
    [Route("api/[controller]")]
    public class EnrollmentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EnrollmentsController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Student")]
        [HttpPost("join")]
        public async Task<IActionResult> JoinClass([FromBody] EnrollRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // 🔥 Tìm lớp bằng mã
            var cls = await _context.ClassSections
                .FirstOrDefaultAsync(c => c.JoinCode == request.ClassCode);

            if (cls == null)
                return BadRequest(new { message = "Class not found with this code" });

            var exist = await _context.Enrollments
                .AnyAsync(e => e.UserId == userId && e.ClassSectionId == cls.Id);

            if (exist)
                return BadRequest(new { message = "Already enrolled" });

            var enroll = new Enrollment
            {
                UserId = userId,
                ClassSectionId = cls.Id
            };

            _context.Enrollments.Add(enroll);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Enrolled successfully" });
        }


        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("class/{classSectionId:int}")]
        public async Task<IActionResult> GetStudentsInClass(int classSectionId)
        {
            var list = await _context.Enrollments
                .Where(e => e.ClassSectionId == classSectionId)
                .Include(e => e.User)
                .Select(e => new
                {
                    e.UserId,
                    e.User.FullName,
                    e.User.UserName,
                    e.User.Email,
                    e.EnrolledAt
                })
                .ToListAsync();

            return Ok(list);
        }

        // Giảng viên/Admin xoá SV khỏi lớp
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("class/{classSectionId:int}/student/{userId}")]
        public async Task<IActionResult> RemoveStudentFromClass(int classSectionId, string userId)
        {
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.ClassSectionId == classSectionId && e.UserId == userId);

            if (enrollment == null)
                return NotFound(new { message = "Student not found in this class" });

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Student removed successfully" });
        }
    }
}
