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
    [Route("api/[controller]")]
    public class ClassSectionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ClassSectionsController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Lấy danh sách lớp mình tham gia / phụ trách
        [Authorize]
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<ClassSectionResponse>>> GetMyClasses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );

            bool isTeacher = roles.Contains("Teacher") || roles.Contains("Admin");

            IQueryable<ClassSection> query;

            if (isTeacher)
            {
                // Giảng viên: lớp do mình phụ trách
                query = _context.ClassSections
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Where(c => c.TeacherId == userId);
            }
            else
            {
                // Sinh viên: lớp mình được enroll
                // Thay đổi: bắt đầu từ ClassSections và lọc bằng Enrollments.Any(...) 
                // để EF tạo JOIN đúng và trả về các ClassSection có Course/Teacher kèm theo.
                query = _context.ClassSections
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Where(c => c.Enrollments.Any(e => e.UserId == userId));
            }

            var list = await query
                .Select(c => new ClassSectionResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    // trong Select(...)
                    JoinCode = c.JoinCode,
                    Room = c.Room,
                    CourseId = c.CourseId,
                    CourseName = c.Course.Name,
                    TeacherId = c.TeacherId,
                    TeacherName = c.Teacher.FullName,
                    IsTeacher = c.TeacherId == userId
                }).ToListAsync();

            return Ok(list);
        }

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ClassSectionResponse>> GetById(int id)
        {
            var c = await _context.ClassSections
                .Include(x => x.Course)
                .Include(x => x.Teacher)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound();

            return Ok(new ClassSectionResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                // trong Select(...)
                JoinCode = c.JoinCode,
                Room = c.Room,
                CourseId = c.CourseId,
                CourseName = c.Course.Name,
                TeacherId = c.TeacherId,
                TeacherName = c.Teacher.FullName
            });
        }
        private async Task<string> GenerateUniqueJoinCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bỏ 0,1,O,I cho dễ đọc
            var rnd = new Random();

            while (true)
            {
                var code = new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[rnd.Next(chars.Length)])
                    .ToArray());

                var exist = await _context.ClassSections
                    .AnyAsync(c => c.JoinCode == code);

                if (!exist)
                    return code;
            }
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassSectionCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var course = await _context.Courses.FindAsync(request.CourseId);
            if (course == null) return BadRequest(new { message = "Course not found" });

            // 🔥 Sinh mã lớp
            var joinCode = await GenerateUniqueJoinCode();

            var cls = new ClassSection
            {
                Name = request.Name,
                Description = request.Description,
                Room = request.Room,
                CourseId = request.CourseId,
                TeacherId = userId,
                JoinCode = joinCode
            };

            _context.ClassSections.Add(cls);
            await _context.SaveChangesAsync();

            await _context.Entry(cls).Reference(x => x.Course).LoadAsync();
            await _context.Entry(cls).Reference(x => x.Teacher).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = cls.Id }, new ClassSectionResponse
            {
                Id = cls.Id,
                Name = cls.Name,
                Description = cls.Description,
                Room = cls.Room,
                CourseId = cls.CourseId,
                CourseName = cls.Course.Name,
                TeacherId = cls.TeacherId,
                TeacherName = cls.Teacher.FullName,
                JoinCode = cls.JoinCode      // ← trả về để gv nhìn và share
            });
        }


        // Chỉ teacher phụ trách hoặc admin được sửa
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassSectionCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var cls = await _context.ClassSections.FindAsync(id);
            if (cls == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );
            bool isAdmin = roles.Contains("Admin");

            if (!isAdmin && cls.TeacherId != userId)
                return Forbid();

            cls.Name = request.Name;
            cls.Description = request.Description;
            cls.Room = request.Room;
            cls.CourseId = request.CourseId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var cls = await _context.ClassSections.FindAsync(id);
            if (cls == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );
            bool isAdmin = roles.Contains("Admin");

            if (!isAdmin && cls.TeacherId != userId)
                return Forbid();

            _context.ClassSections.Remove(cls);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
