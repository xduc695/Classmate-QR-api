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


        // Giảng viên/Admin xem sinh viên trong lớp
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
        [HttpDelete("{classSectionId:int}/{userId}")]
        public async Task<IActionResult> RemoveFromClass(int classSectionId, string userId)
        {
            var e = await _context.Enrollments
                .FirstOrDefaultAsync(x => x.ClassSectionId == classSectionId && x.UserId == userId);

            if (e == null) return NotFound();

            _context.Enrollments.Remove(e);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
