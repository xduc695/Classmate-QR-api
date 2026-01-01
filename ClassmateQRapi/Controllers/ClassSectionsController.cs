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
                query = _context.ClassSections
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Where(c => c.TeacherId == userId);
            }
            else
            {
                query = _context.ClassSections
                    .Include(c => c.Course)
                    .Include(c => c.Teacher)
                    .Where(c => c.Enrollments.Any(e => e.UserId == userId));
            }

            // Lấy dữ liệu và chuyển đổi
            var classSections = await query.ToListAsync();
            var result = new List<ClassSectionResponse>();

            foreach (var c in classSections)
            {
                var studyDaysList = ConvertBitmaskToDays(c.StudyDays);

                // Lấy số lượng sinh viên
                var studentCount = await _context.Enrollments
                    .CountAsync(e => e.ClassSectionId == c.Id);

                result.Add(new ClassSectionResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Room = c.Room,
                    CourseId = c.CourseId,
                    CourseName = c.Course?.Name ?? "Unknown",
                    TeacherId = c.TeacherId,
                    TeacherName = c.Teacher?.FullName ?? "Unknown",
                    JoinCode = c.JoinCode,
                    IsTeacher = c.TeacherId == userId,
                    // 🔥 QUAN TRỌNG: Thêm đầy đủ các trường
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    StudyDays = studyDaysList,
                    StudentCount = studentCount, // 🔥 THÊM STUDENT COUNT
                    // IsActive sẽ tự động tính khi serialize (computed property)
                    ScheduleSummary = GenerateScheduleSummary(
                        c.StartDate, c.EndDate, studyDaysList, c.StartTime, c.EndTime
                    )
                });
            }

            return Ok(result);
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

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var studyDaysList = ConvertBitmaskToDays(c.StudyDays);

            // Lấy số lượng sinh viên
            var studentCount = await _context.Enrollments
                .CountAsync(e => e.ClassSectionId == c.Id);

            return Ok(new ClassSectionResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Room = c.Room,
                CourseId = c.CourseId,
                CourseName = c.Course?.Name ?? "Unknown",
                TeacherId = c.TeacherId,
                TeacherName = c.Teacher?.FullName ?? "Unknown",
                JoinCode = c.JoinCode,
                IsTeacher = c.TeacherId == userId,
                // 🔥 THÊM ĐẦY ĐỦ CÁC TRƯỜNG
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                StudyDays = studyDaysList,
                StudentCount = studentCount, // 🔥 THÊM STUDENT COUNT
                // IsActive sẽ tự động tính khi serialize (computed property)
                ScheduleSummary = GenerateScheduleSummary(
                    c.StartDate, c.EndDate, studyDaysList, c.StartTime, c.EndTime
                )
            });
        }

        // Phương thức chuyển đổi bitmask thành danh sách
        private List<int> ConvertBitmaskToDays(int bitmask)
        {
            var days = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                if ((bitmask & (1 << i)) != 0)
                {
                    days.Add(i);
                }
            }
            return days;
        }

        // Phương thức tạo schedule summary
        private string GenerateScheduleSummary(DateTime startDate, DateTime endDate,
            List<int> studyDays, TimeSpan startTime, TimeSpan endTime)
        {
            if (studyDays == null || studyDays.Count == 0)
                return "Chưa có lịch học";

            var daysOfWeek = new[] { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

            var studyDayNames = studyDays
                .OrderBy(d => d)
                .Select(d => daysOfWeek[d])
                .ToList();

            var dayString = studyDayNames.Count == 1
                ? studyDayNames.First()
                : string.Join(", ", studyDayNames.Take(studyDayNames.Count - 1)) +
                  (studyDayNames.Count > 1 ? " và " + studyDayNames.Last() : "");

            return $"Học {dayString}, từ {startTime:hh\\:mm} - {endTime:hh\\:mm}, " +
                   $"từ {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy}";
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

            // Validate dates
            if (request.StartDate >= request.EndDate)
                return BadRequest(new { message = "Ngày bắt đầu phải trước ngày kết thúc" });

            if (request.StartDate < DateTime.Today)
                return BadRequest(new { message = "Ngày bắt đầu không được ở quá khứ" });

            if (request.EndDate > DateTime.Today.AddYears(1))
                return BadRequest(new { message = "Ngày kết thúc không được vượt quá 1 năm" });

            // Validate study days
            if (request.StudyDays == null || !request.StudyDays.Any())
                return BadRequest(new { message = "Vui lòng chọn ít nhất một thứ học" });

            foreach (var day in request.StudyDays)
            {
                if (day < 0 || day > 6)
                    return BadRequest(new { message = $"Thứ học không hợp lệ: {day}" });
            }

            // Validate time
            if (request.StartTime >= request.EndTime)
                return BadRequest(new { message = "Giờ bắt đầu phải trước giờ kết thúc" });

            if (request.EndTime - request.StartTime > TimeSpan.FromHours(6))
                return BadRequest(new { message = "Thời gian học không được quá 6 giờ" });

            // 🔥 Sinh mã lớp
            var joinCode = await GenerateUniqueJoinCode();

            // Convert study days list to bitmask
            int studyDaysBitmask = 0;
            foreach (var day in request.StudyDays.Distinct())
            {
                studyDaysBitmask |= (1 << day);
            }

            var cls = new ClassSection
            {
                Name = request.Name,
                Description = request.Description,
                Room = request.Room,
                CourseId = request.CourseId,
                TeacherId = userId,
                JoinCode = joinCode,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                StudyDays = studyDaysBitmask,
                StartTime = request.StartTime,
                EndTime = request.EndTime
            };

            _context.ClassSections.Add(cls);
            await _context.SaveChangesAsync();

            await _context.Entry(cls).Reference(x => x.Course).LoadAsync();
            await _context.Entry(cls).Reference(x => x.Teacher).LoadAsync();

            // Convert bitmask back to list for response
            var studyDaysList = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                if ((cls.StudyDays & (1 << i)) != 0)
                {
                    studyDaysList.Add(i);
                }
            }

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
                JoinCode = cls.JoinCode,
                StartDate = cls.StartDate,
                EndDate = cls.EndDate,
                StudyDays = studyDaysList,
                StartTime = cls.StartTime,
                EndTime = cls.EndTime,
                ScheduleSummary = GenerateScheduleSummary(
                    cls.StartDate, cls.EndDate, studyDaysList, cls.StartTime, cls.EndTime
                )
                // IsActive sẽ tự động tính khi serialize (computed property)
            });
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassSectionCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var cls = await _context.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cls == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(userId)
            );
            bool isAdmin = roles.Contains("Admin");

            if (!isAdmin && cls.TeacherId != userId)
                return Forbid();

            // Validate dates
            if (request.StartDate >= request.EndDate)
                return BadRequest(new { message = "Ngày bắt đầu phải trước ngày kết thúc" });

            if (request.StartDate < DateTime.Today)
                return BadRequest(new { message = "Ngày bắt đầu không được ở quá khứ" });

            // Validate study days
            if (request.StudyDays == null || !request.StudyDays.Any())
                return BadRequest(new { message = "Vui lòng chọn ít nhất một thứ học" });

            // Validate time
            if (request.StartTime >= request.EndTime)
                return BadRequest(new { message = "Giờ bắt đầu phải trước giờ kết thúc" });

            // Update
            cls.Name = request.Name;
            cls.Description = request.Description;
            cls.Room = request.Room;
            cls.CourseId = request.CourseId;
            cls.StartDate = request.StartDate;
            cls.EndDate = request.EndDate;
            cls.StartTime = request.StartTime;
            cls.EndTime = request.EndTime;

            // Update StudyDays
            int studyDaysBitmask = 0;
            foreach (var day in request.StudyDays.Distinct())
            {
                studyDaysBitmask |= (1 << day);
            }
            cls.StudyDays = studyDaysBitmask;

            await _context.SaveChangesAsync();

            // Trả về response đầy đủ
            var studyDaysList = ConvertBitmaskToDays(cls.StudyDays);

            // Lấy số lượng sinh viên
            var studentCount = await _context.Enrollments
                .CountAsync(e => e.ClassSectionId == cls.Id);

            return Ok(new ClassSectionResponse
            {
                Id = cls.Id,
                Name = cls.Name,
                Description = cls.Description,
                Room = cls.Room,
                CourseId = cls.CourseId,
                CourseName = cls.Course?.Name ?? "Unknown",
                TeacherId = cls.TeacherId,
                TeacherName = cls.Teacher?.FullName ?? "Unknown",
                JoinCode = cls.JoinCode,
                IsTeacher = cls.TeacherId == userId,
                StartDate = cls.StartDate,
                EndDate = cls.EndDate,
                StudyDays = studyDaysList,
                StartTime = cls.StartTime,
                EndTime = cls.EndTime,
                StudentCount = studentCount, // 🔥 THÊM STUDENT COUNT
                ScheduleSummary = GenerateScheduleSummary(
                    cls.StartDate, cls.EndDate, studyDaysList, cls.StartTime, cls.EndTime
                )
                // IsActive sẽ tự động tính khi serialize (computed property)
            });
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

            return Ok(new { message = "Xóa lớp học thành công" });
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("{id:int}/students")]
        public async Task<ActionResult<IEnumerable<object>>> GetStudentsInClass(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Kiểm tra lớp có tồn tại không
            var cls = await _context.ClassSections.FindAsync(id);
            if (cls == null) return NotFound("Lớp học phần không tồn tại.");

            // 2. Kiểm tra quyền (GV phụ trách hoặc Admin)
            var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
            bool isAdmin = roles.Contains("Admin");

            if (!isAdmin && cls.TeacherId != userId)
            {
                return Forbid(); // 403 Forbidden
            }

            // 3. Lấy danh sách sinh viên từ bảng Enrollments
            var students = await _context.Enrollments
                .Where(e => e.ClassSectionId == id)
                .Include(e => e.User)
                .Select(e => new
                {
                    StudentId = e.UserId,
                    FullName = e.User.FullName,
                    Email = e.User.Email,
                    StudentCode = e.User.UserName,
                })
                .ToListAsync();

            return Ok(students);
        }
    }
}