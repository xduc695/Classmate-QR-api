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

            //  SỬA LOGIC THỜI GIAN TẠI ĐÂY
            // Lấy giờ hiện tại (Server tự quyết định, không tin tưởng Client)
            var now = DateTime.UtcNow;

            var session = new AttendanceSession
            {
                ClassSectionId = request.ClassSectionId,

                // Server tự tính toán thời gian
                StartTime = now,
                EndTime = now.AddMinutes(request.Minutes), // Cộng số phút vào giờ hiện tại

                Code = code,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                session.Id,
                session.ClassSectionId,
                session.StartTime,
                session.EndTime,
                session.Code
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

                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();
            // TÍNH KHOẢNG CÁCH TỪ ĐIỂM DANH ĐẾN VỊ TRÍ CỦA GIẢNG VIÊN
            double finalDistance = Math.Round(CalculateDistance(session.Latitude, session.Longitude, request.Latitude, request.Longitude), 1);
            return Ok(new
            {
                message = "Check-in success",
                distance = finalDistance
            });
        }
        //hàm tính khoảng cách giữa 2 điểm (lat1, lon1) và (lat2, lon2) theo đơn vị km
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Bán kính trái đất xấp xỉ 6371 km = 6371000 mét
            double R = 6371000;

            // Chuyển đổi độ sang radian
            double dLat = (lat2 - lat1) * (Math.PI / 180);
            double dLon = (lon2 - lon1) * (Math.PI / 180);

            // Áp dụng công thức
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * (Math.PI / 180)) * Math.Cos(lat2 * (Math.PI / 180)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            // Trả về khoảng cách
            return R * c;
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
                    r.Latitude,
                    r.Longitude
                }).ToListAsync();
            //  TÍNH KHOẢNG CÁCH NGAY TẠI SERVER ĐỂ GỬI VỀ FRONTEND DỄ HIỂN THỊ
            var resultRecords = records.Select(r => new
            {
                r.UserId,
                r.FullName,
                r.UserName,
                r.CheckedInAt,
                r.Latitude,
                r.Longitude,
                // Tính khoảng cách từ chỗ SV ngồi đến chỗ GV (Session)
                Distance = Math.Round(CalculateDistance(session.Latitude, session.Longitude, r.Latitude, r.Longitude), 1)
            });

            return Ok(new
            {
                session.Id,
                session.ClassSectionId,
                session.Code,
                session.StartTime,
                session.EndTime,
                TotalChecked = records.Count,
                Records = resultRecords
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
                    CourseName = r.AttendanceSession.ClassSection.Course.Name
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
                endTime = session.EndTime
            });
        }
    }

}

