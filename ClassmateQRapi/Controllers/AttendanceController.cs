using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using ClosedXML.Excel;
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

        // Sinh viên check-in bằng Code (từ QR) - LOGIC ĐƠN GIẢN
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

            // Kiểm tra sinh viên có trong lớp không
            var enrolled = await _context.Enrollments
                .AnyAsync(e => e.UserId == userId && e.ClassSectionId == session.ClassSectionId);

            if (!enrolled)
                return BadRequest(new { message = "You are not in this class" });

            // Kiểm tra đã điểm danh chưa
            var exist = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == session.Id && r.UserId == userId);

            if (exist)
                return BadRequest(new { message = "You have already checked in" });

            // Xác định status dựa trên thời gian - LOGIC ĐƠN GIẢN
            string status;
            string message;

            if (now < session.StartTime)
            {
                return BadRequest(new { message = "Attendance session has not started yet" });
            }
            else if (now > session.EndTime)
            {
                // Nếu quá thời gian EndTime -> tự động LATE
                status = "LATE";
                message = "Check-in success (Late - after session ended)";
            }
            else
            {
                // Trong khoảng thời gian cho phép -> OK
                status = "OK";
                message = "Check-in success";
            }

            // Tạo record với status
            var record = new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                UserId = userId,
                CheckedInAt = now,
                StudentLocation = request.StudentLocation,
                Status = status
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = message,
                location = record.StudentLocation,
                status = status,
                checkInTime = now,
                sessionEndTime = session.EndTime
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
                    r.StudentLocation,
                    r.Status
                }).ToListAsync();

            // Tính toán thống kê
            var totalStudents = await _context.Enrollments
                .Where(e => e.ClassSectionId == session.ClassSectionId)
                .CountAsync();

            var onTimeCount = records.Count(r => r.Status == "OK");
            var lateCount = records.Count(r => r.Status == "LATE");
            var absentCount = totalStudents - records.Count;

            return Ok(new
            {
                session.Id,
                session.ClassSectionId,
                session.Code,
                session.StartTime,
                session.EndTime,
                session.TeacherLocation,
                TotalChecked = records.Count,
                Statistics = new
                {
                    TotalStudents = totalStudents,
                    OnTime = onTimeCount,
                    Late = lateCount,
                    Absent = absentCount
                },
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
                    r.Status,
                    SessionId = r.AttendanceSessionId,
                    r.AttendanceSession.Code,
                    ClassId = r.AttendanceSession.ClassSectionId,
                    ClassName = r.AttendanceSession.ClassSection.Name,
                    CourseName = r.AttendanceSession.ClassSection.Course.Name,
                    TeacherLocation = r.AttendanceSession.TeacherLocation,
                    StudentLocation = r.StudentLocation,
                    SessionStart = r.AttendanceSession.StartTime,
                    SessionEnd = r.AttendanceSession.EndTime
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
            string expectedStatus = "";

            // Xác định expected status
            if (now < session.StartTime)
                expectedStatus = "NOT_STARTED";
            else if (now > session.EndTime)
                expectedStatus = "LATE";
            else
                expectedStatus = "OK";

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
                teacherLocation = session.TeacherLocation,
                expectedStatus
            });
        }

        // Lấy danh sách sessions theo class section
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

        // API để lấy thống kê chi tiết cho giảng viên
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("sessions/{sessionId:int}/stats")]
        public async Task<IActionResult> GetSessionStats(int sessionId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.ClassSection)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return NotFound();

            var totalStudents = await _context.Enrollments
                .Where(e => e.ClassSectionId == session.ClassSectionId)
                .CountAsync();

            var records = await _context.AttendanceRecords
                .Where(r => r.AttendanceSessionId == sessionId)
                .ToListAsync();

            var onTimeCount = records.Count(r => r.Status == "OK");
            var lateCount = records.Count(r => r.Status == "LATE");
            var absentCount = totalStudents - records.Count;

            return Ok(new
            {
                Session = new
                {
                    session.Id,
                    session.Code,
                    session.StartTime,
                    session.EndTime,
                    session.TeacherLocation,
                    ClassName = session.ClassSection?.Name
                },
                Statistics = new
                {
                    TotalStudents = totalStudents,
                    Attended = records.Count,
                    OnTime = onTimeCount,
                    Late = lateCount,
                    Absent = absentCount,
                    AttendanceRate = totalStudents > 0 ? (records.Count * 100.0 / totalStudents) : 0
                },
                StatusBreakdown = new
                {
                    OK = onTimeCount,
                    LATE = lateCount,
                    ABSENT = absentCount
                }
            });
        }
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet("sessions/{sessionId:int}/export-excel")]
        public async Task<IActionResult> ExportAttendanceToExcel(int sessionId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.ClassSection)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found" });

            // Lấy tất cả sinh viên trong lớp
            var allStudents = await _context.Enrollments
                .Where(e => e.ClassSectionId == session.ClassSectionId)
                .Include(e => e.User)
                .Select(e => new
                {
                    e.UserId,
                    e.User.UserName,
                    e.User.FullName
                })
                .ToListAsync();

            // Lấy danh sách đã điểm danh
            var attendanceRecords = await _context.AttendanceRecords
                .Where(r => r.AttendanceSessionId == sessionId)
                .Include(r => r.User)
                .Select(r => new
                {
                    r.UserId,
                    r.Status,
                    CheckedInAt = r.CheckedInAt
                })
                .ToListAsync();

            // Tạo workbook Excel
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Attendance");

            // Định dạng tiêu đề
            var title = worksheet.Cell(1, 1);
            title.Value = $"ATTENDANCE REPORT - Session: {session.Code}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 16;
            worksheet.Range(1, 1, 1, 4).Merge();

            // Thông tin session
            worksheet.Cell(2, 1).Value = "Class:";
            worksheet.Cell(2, 2).Value = session.ClassSection?.Name;
            worksheet.Cell(3, 1).Value = "Session Code:";
            worksheet.Cell(3, 2).Value = session.Code;
            worksheet.Cell(4, 1).Value = "Time:";
            worksheet.Cell(4, 2).Value = $"{session.StartTime:dd/MM/yyyy HH:mm} - {session.EndTime:dd/MM/yyyy HH:mm}";
            worksheet.Cell(5, 1).Value = "Location:";
            worksheet.Cell(5, 2).Value = session.TeacherLocation;

            // Tạo header cho dữ liệu
            var headersRow = 7;
            worksheet.Cell(headersRow, 1).Value = "No.";
            worksheet.Cell(headersRow, 2).Value = "ID";
            worksheet.Cell(headersRow, 3).Value = "Username";
            worksheet.Cell(headersRow, 4).Value = "Full Name";
            worksheet.Cell(headersRow, 5).Value = "Status";
            worksheet.Cell(headersRow, 6).Value = "Check-in Time";

            // Định dạng header
            var headerRange = worksheet.Range(headersRow, 1, headersRow, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // Điền dữ liệu
            var currentRow = headersRow + 1;
            var index = 1;

            foreach (var student in allStudents)
            {
                var record = attendanceRecords.FirstOrDefault(r => r.UserId == student.UserId);
                var status = record?.Status ?? "ABSENT";
                var checkInTime = record?.CheckedInAt;

                worksheet.Cell(currentRow, 1).Value = index;
                worksheet.Cell(currentRow, 2).Value = student.UserId;
                worksheet.Cell(currentRow, 3).Value = student.UserName;
                worksheet.Cell(currentRow, 4).Value = student.FullName;
                worksheet.Cell(currentRow, 5).Value = status;

                if (checkInTime.HasValue)
                {
                    worksheet.Cell(currentRow, 6).Value = checkInTime.Value.ToString("dd/MM/yyyy HH:mm:ss");
                }
                else
                {
                    worksheet.Cell(currentRow, 6).Value = "-";
                }

                // Định dạng border cho dòng
                var rowRange = worksheet.Range(currentRow, 1, currentRow, 6);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
                index++;
            }

            // Tự động điều chỉnh độ rộng cột
            worksheet.Columns().AdjustToContents();

            // Tính thống kê
            var statsRow = currentRow + 2;
            worksheet.Cell(statsRow, 1).Value = "STATISTICS";
            worksheet.Cell(statsRow, 1).Style.Font.Bold = true;
            worksheet.Cell(statsRow, 1).Style.Font.FontSize = 14;

            statsRow++;
            worksheet.Cell(statsRow, 1).Value = "Total Students:";
            worksheet.Cell(statsRow, 2).Value = allStudents.Count;

            statsRow++;
            worksheet.Cell(statsRow, 1).Value = "Present:";
            worksheet.Cell(statsRow, 2).Value = attendanceRecords.Count(r => r.Status == "OK");

            statsRow++;
            worksheet.Cell(statsRow, 1).Value = "Late:";
            worksheet.Cell(statsRow, 2).Value = attendanceRecords.Count(r => r.Status == "LATE");

            statsRow++;
            worksheet.Cell(statsRow, 1).Value = "Absent:";
            worksheet.Cell(statsRow, 2).Value = allStudents.Count - attendanceRecords.Count;

            statsRow++;
            var attendanceRate = allStudents.Count > 0 ?
                (attendanceRecords.Count * 100.0 / allStudents.Count) : 0;
            worksheet.Cell(statsRow, 1).Value = "Attendance Rate:";
            worksheet.Cell(statsRow, 2).Value = $"{attendanceRate:F2}%";

            // Tạo memory stream
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            // Trả về file
            var fileName = $"Attendance_{session.Code}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}