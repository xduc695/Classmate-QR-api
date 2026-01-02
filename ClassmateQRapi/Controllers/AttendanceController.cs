// AttendanceController.cs - Sửa các phương thức
using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttendanceController(AppDbContext context)
        {
            _context = context;
        }

        // Giảng viên tạo buổi điểm danh -> sinh Code
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] CreateAttendanceSessionRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var cls = await _context.ClassSections.FindAsync(request.ClassSectionId);
            if (cls == null) return BadRequest(new { message = "ClassSection not found" });

            var code = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            var now = DateTime.UtcNow;

            var session = new AttendanceSession
            {
                ClassSectionId = request.ClassSectionId,
                StartTime = now,
                EndTime = now.AddMinutes(request.Minutes),
                Code = code,
                TeacherLocation = request.TeacherLocation
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                session.Id,
                session.ClassSectionId,
                session.StartTime,
                session.EndTime,
                session.Code,
                session.TeacherLocation
            });
        }

        // Sinh viên check-in bằng Code (từ QR)
        [Authorize(Roles = "Student")]
        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var now = DateTime.UtcNow;

            var session = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.Code == request.Code);

            if (session == null)
                return BadRequest(new { message = "Invalid code" });

            if (now < session.StartTime || now > session.EndTime)
                return BadRequest(new { message = "Attendance time is over or not started" });

            var enrolled = await _context.Enrollments
                .AnyAsync(e => e.UserId == userId && e.ClassSectionId == session.ClassSectionId);

            if (!enrolled)
                return BadRequest(new { message = "You are not in this class" });

            var exist = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == session.Id && r.UserId == userId);

            if (exist)
                return BadRequest(new { message = "You have already checked in" });

            var record = new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                UserId = userId,
                CheckedInAt = now,
                StudentLocation = request.StudentLocation
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            // Xóa hàm CalculateDistance vì không cần tính khoảng cách nữa
            return Ok(new
            {
                message = "Check-in success",
                location = record.StudentLocation
            });
        }

        // Giảng viên xem lịch sử điểm danh 1 buổi
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("sessions/{sessionId:int}/records")]
        public async Task<IActionResult> GetRecords(int sessionId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.ClassSection)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return NotFound();

            var records = await _context.AttendanceRecords
                .Where(r => r.AttendanceSessionId == sessionId)
                .Include(r => r.User)
                .Select(r => new
                {
                    r.UserId,
                    r.User.FullName,
                    r.User.UserName,
                    r.CheckedInAt,
                    r.StudentLocation
                }).ToListAsync();

            return Ok(new
            {
                session.Id,
                session.ClassSectionId,
                session.Code,
                session.StartTime,
                session.EndTime,
                session.TeacherLocation,
                TotalChecked = records.Count,
                Records = records
            });
        }

        // Sinh viên xem lịch sử điểm danh của mình
        [Authorize(Roles = "Student")]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyAttendance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var list = await _context.AttendanceRecords
                .Where(r => r.UserId == userId)
                .Include(r => r.AttendanceSession)
                    .ThenInclude(s => s.ClassSection)
                        .ThenInclude(c => c.Course)
                .Select(r => new
                {
                    r.Id,
                    r.CheckedInAt,
                    SessionId = r.AttendanceSessionId,
                    r.AttendanceSession.Code,
                    ClassId = r.AttendanceSession.ClassSectionId,
                    ClassName = r.AttendanceSession.ClassSection.Name,
                    CourseName = r.AttendanceSession.ClassSection.Course.Name,
                    TeacherLocation = r.AttendanceSession.TeacherLocation,
                    StudentLocation = r.StudentLocation
                })
                .ToListAsync();

            return Ok(list);
        }

        // Validate attendance code
        [Authorize]
        [HttpGet("sessions/validate")]
        public async Task<IActionResult> ValidateCode([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { valid = false, message = "Code is required" });

            var session = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.Code == code);

            if (session == null)
                return NotFound(new { valid = false, message = "Invalid code" });

            var now = DateTime.UtcNow;
            bool isActive = now >= session.StartTime && now <= session.EndTime;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isEnrolled = false;
            bool alreadyCheckedIn = false;

            if (!string.IsNullOrEmpty(userId))
            {
                isEnrolled = await _context.Enrollments
                    .AnyAsync(e => e.UserId == userId && e.ClassSectionId == session.ClassSectionId);

                alreadyCheckedIn = await _context.AttendanceRecords
                    .AnyAsync(r => r.AttendanceSessionId == session.Id && r.UserId == userId);
            }

            var anyRecords = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == session.Id);

            return Ok(new
            {
                valid = true,
                sessionId = session.Id,
                classSectionId = session.ClassSectionId,
                isActive,
                isEnrolled,
                alreadyCheckedIn,
                anyRecords,
                startTime = session.StartTime,
                endTime = session.EndTime,
                teacherLocation = session.TeacherLocation
            });
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("sessions/class/{classSectionId:int}")]
        public async Task<IActionResult> GetSessionsByClass(int classSectionId)
        {
            var sessions = await _context.AttendanceSessions
                .Where(s => s.ClassSectionId == classSectionId)
                .OrderByDescending(s => s.StartTime)
                .Select(s => new
                {
                    s.Id,
                    s.Code,
                    s.StartTime,
                    s.EndTime,
                    s.CreatedAt,
                    s.TeacherLocation,
                    CheckedInCount = _context.AttendanceRecords.Count(r => r.AttendanceSessionId == s.Id)
                })
                .ToListAsync();

            return Ok(sessions);
        }

        // Xóa session điểm danh
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("sessions/{sessionId:int}")]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            var exists = await _context.AttendanceSessions
                .AnyAsync(s => s.Id == sessionId);

            if (!exists)
                return NotFound(new { message = "Session not found or already deleted" });

            _context.AttendanceSessions.Remove(new AttendanceSession { Id = sessionId });

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Session deleted successfully" });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "Session was already deleted by another request"
                });
            }
        }


        // Kiểm tra session có còn active không
        [Authorize]
        [HttpGet("sessions/{sessionId:int}/status")]
        public async Task<IActionResult> GetSessionStatus(int sessionId)
        {
            var session = await _context.AttendanceSessions.FindAsync(sessionId);
            if (session == null) return NotFound();

            var now = DateTime.UtcNow;
            bool isActive = now >= session.StartTime && now <= session.EndTime;
            bool isExpired = now > session.EndTime;

            return Ok(new
            {
                session.Id,
                session.Code,
                session.StartTime,
                session.EndTime,
                session.TeacherLocation,
                isActive,
                isExpired,
                remainingSeconds = isActive ? (int)(session.EndTime - now).TotalSeconds : 0
            });
        }
    }
}