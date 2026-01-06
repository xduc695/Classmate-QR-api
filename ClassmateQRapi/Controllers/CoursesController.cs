using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.DTOs;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CoursesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Ai cũng xem được
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CourseResponse>>> GetAll()
        {
            var courses = await _context.Courses
                .Select(c => new CourseResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    Description = c.Description
                })
                .ToListAsync();

            return Ok(courses);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CourseResponse>> GetById(int id)
        {
            var c = await _context.Courses.FindAsync(id);
            if (c == null) return NotFound();

            return Ok(new CourseResponse
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                Description = c.Description
            });
        }

        // Chỉ Teacher/Admin được tạo
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CourseCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var exist = await _context.Courses.AnyAsync(x => x.Code == request.Code);
            if (exist) return BadRequest(new { message = "Course code already exists" });

            var course = new Course
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = course.Id }, new CourseResponse
            {
                Id = course.Id,
                Name = course.Name,
                Code = course.Code,
                Description = course.Description
            });
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CourseCreateRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var c = await _context.Courses.FindAsync(id);
            if (c == null) return NotFound();

            var exist = await _context.Courses
                .AnyAsync(x => x.Code == request.Code && x.Id != id);
            if (exist) return BadRequest(new { message = "Course code already exists" });

            c.Name = request.Name;
            c.Code = request.Code;
            c.Description = request.Description;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _context.Courses.FindAsync(id);
            if (c == null) return NotFound();

            _context.Courses.Remove(c);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        private string GetSafeFolderName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "General";

            // 1. Chuẩn hóa unicode để tách dấu ra khỏi ký tự gốc
            string normalizedString = text.Normalize(NormalizationForm.FormD);

            // 2. Dùng Regex để loại bỏ dấu
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // 3. Trả về dạng form C, loại bỏ ký tự đặc biệt và khoảng trắng
            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            // 4. Giữ lại chữ cái và số, bỏ hết dấu cách và ký tự lạ
            return Regex.Replace(result, "[^a-zA-Z0-9]", "");
        }
        // [POST] api/courses/{id}/materials
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/materials")]
        public async Task<IActionResult> AddMaterial(int id, [FromForm] MaterialCreateRequest request)
        {
            // 1. Lấy thông tin khóa học
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound("Không tìm thấy môn học");

            var resource = new CourseResource
            {
                CourseId = id,
                Title = request.Title,
                Description = request.Description ?? "Tài liệu môn học",
                ResourceFiles = new List<CourseResourceFile>(),
                CreatedAt = DateTime.UtcNow
            };

            if (request.Files != null && request.Files.Any())
            {
                // --- 🔥 SỬA TẠI ĐÂY: TẠO FOLDER THEO TÊN MÔN ---

                // 1. Tạo tên thư mục an toàn (VD: PhapLuatDaiCuong)
                string subFolderName = GetSafeFolderName(course.Name);

                // 2. Đường dẫn vật lý: Root/CourseMaterials/PhapLuatDaiCuong
                var folder = Path.Combine(_env.ContentRootPath, "CourseMaterials", subFolderName);

                // 3. Tạo thư mục nếu chưa có
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Files)
                {
                    // Giữ tên file gốc hoặc thêm Guid để tránh trùng
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // 4. Lưu đường dẫn tương đối vào DB (Quan trọng)
                    // Backend sẽ trả về: /coursematerials/PhapLuatDaiCuong/filename.pdf
                    resource.ResourceFiles.Add(new CourseResourceFile
                    {
                        FileName = file.FileName,
                        // Lưu ý: Dấu gạch chéo '/' để đúng chuẩn URL
                        FileUrl = $"/coursematerials/{subFolderName}/{fileName}"
                    });
                }
            }

            _context.CourseResources.Add(resource);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã thêm tài liệu thành công" });
        }
    }
}
