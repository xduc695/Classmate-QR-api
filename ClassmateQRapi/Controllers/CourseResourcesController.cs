using ClassMate.Api.DTOs;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api")]
    public class CourseResourcesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CourseResourcesController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost("courses/{courseId}/resources")]
        public async Task<IActionResult> UploadResource(int courseId, [FromForm] CourseResourceCreateRequest request)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound("Course không tồn tại");

            var res = new CourseResource
            {
                CourseId = courseId,
                Title = request.Title,
                Description = request.Description,
                LinkUrl = request.LinkUrl,
                ResourceFiles = new List<CourseResourceFile>()
            };

            if (request.Files != null && request.Files.Any())
            {
                // Trỏ vào thư mục 'CourseResources' ở gốc dự án
                var folder = Path.Combine(_env.ContentRootPath, "CourseResources");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Files)
                {
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    res.ResourceFiles.Add(new CourseResourceFile
                    {
                        FileName = file.FileName,
                        FileUrl = $"/courseresources/{fileName}"
                    });
                }
            }

            _context.CourseResources.Add(res);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tải lên tài liệu thành công" });
        }

        [Authorize]
        [HttpGet("courses/{courseId}/resources")]
        public async Task<IActionResult> GetResources(int courseId)
        {
            var exists = await _context.Courses.AnyAsync(c => c.Id == courseId);
            if (!exists) return NotFound();

            var data = await _context.CourseResources
                .Include(r => r.ResourceFiles) // Load danh sách file
                .Where(r => r.CourseId == courseId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new CourseResourceResponse
                {
                    Id = r.Id,
                    Title = r.Title,
                    Description = r.Description,
                    LinkUrl = r.LinkUrl,
                    CreatedAt = r.CreatedAt,
                    Files = r.ResourceFiles.Select(f => new FileDto
                    {
                        Id = f.Id,
                        FileName = f.FileName,
                        FileUrl = f.FileUrl
                    }).ToList()
                })
                .ToListAsync();

            return Ok(data);
        }

        [Authorize(Roles = "Teacher,Admin")]
        [HttpPut("courseresources/{id}")]
        public async Task<IActionResult> UpdateResource(int id, [FromForm] CourseResourceCreateRequest request)
        {
            var res = await _context.CourseResources
                .Include(r => r.ResourceFiles)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (res == null) return NotFound("Tài liệu không tồn tại");

            // Cập nhật thông tin text
            res.Title = request.Title;
            res.Description = request.Description;
            res.LinkUrl = request.LinkUrl;

            // Xử lý thêm file mới (nếu giáo viên chọn thêm)
            if (request.Files != null && request.Files.Any())
            {
                var folder = Path.Combine(_env.ContentRootPath, "CourseResources");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                foreach (var file in request.Files)
                {
                    var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    res.ResourceFiles.Add(new CourseResourceFile
                    {
                        FileName = file.FileName,
                        FileUrl = $"/courseresources/{fileName}"
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật tài liệu thành công" });
        }
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("resources/files/{fileId}")]
        public async Task<IActionResult> DeleteSingleFile(int fileId)
        {
            var file = await _context.CourseResourceFiles.FindAsync(fileId);
            if (file == null) return NotFound("File không tồn tại.");

            // Xóa file vật lý
            var filePath = Path.Combine(_env.ContentRootPath, "CourseResources", Path.GetFileName(file.FileUrl));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.CourseResourceFiles.Remove(file);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa file thành công" });
        }
        [Authorize(Roles = "Teacher,Admin")]
        [HttpDelete("courseresources/{id}")]
        public async Task<IActionResult> DeleteResource(int id)
        {
            var res = await _context.CourseResources
                .Include(r => r.ResourceFiles) // Include để lấy list file xóa khỏi ổ cứng
                .FirstOrDefaultAsync(r => r.Id == id);

            if (res == null) return NotFound("Tài liệu không tồn tại");

            // 1. Xóa file vật lý trên server để dọn dẹp bộ nhớ
            if (res.ResourceFiles != null)
            {
                foreach (var file in res.ResourceFiles)
                {
                    // Chuyển đường dẫn URL thành đường dẫn vật lý
                    var filePath = Path.Combine(_env.ContentRootPath, file.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }

            // 2. Xóa dữ liệu trong DB
            _context.CourseResources.Remove(res);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa tài liệu" });
        }
    }
}
