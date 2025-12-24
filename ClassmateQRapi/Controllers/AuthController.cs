using ClassMate.Api.DTOs;
using ClassmateQRapi.DTOs;
using ClassmateQRapi.Entities;
using ClassmateQRapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClassmateQRapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AuthController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtTokenService jwtTokenService,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenService = jwtTokenService;
            _config = config;
            _env = env;
        }

        // ==========================
        //  ĐĂNG KÝ + UPLOAD AVATAR
        // ==========================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existUser = await _userManager.FindByNameAsync(request.UserName);
            if (existUser != null)
                return BadRequest(new { message = "Username already exists" });

            var existEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existEmail != null)
                return BadRequest(new { message = "Email already exists" });

            var user = new AppUser
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.UtcNow
            };

            // Tạo user trước
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Register failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            // Nếu có avatar -> lưu file
            if (request.Avatar != null && request.Avatar.Length > 0)
            {
                var avatarsFolder = Path.Combine(_env.ContentRootPath, "Avatars");
                Directory.CreateDirectory(avatarsFolder);

                var ext = Path.GetExtension(request.Avatar.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                var fileName = $"{user.Id}{ext}";
                var filePath = Path.Combine(avatarsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Avatar.CopyToAsync(stream);
                }

                // Lưu đường dẫn tương đối cho FE
                user.AvatarUrl = $"/avatars/{fileName}";
                await _userManager.UpdateAsync(user);
            }

            // Gán role
            var roleName = string.IsNullOrWhiteSpace(request.Role) ? "Student" : request.Role;

            if (!await _roleManager.RoleExistsAsync(roleName))
                return BadRequest(new { message = $"Role {roleName} does not exist" });

            await _userManager.AddToRoleAsync(user, roleName);

            // Tạo token
            var token = _jwtTokenService.GenerateToken(user, roleName);

            var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"] ?? "60");
            var expiration = DateTime.UtcNow.AddMinutes(expiresMinutes);

            return Ok(new
            {
                token,
                expiration,
                userName = user.UserName,
                fullName = user.FullName,
                role = roleName,
                avatarUrl = user.AvatarUrl
            });
        }

        // ==========================
        //  LOGIN (CÓ RATE LIMIT)
        // ==========================
        [EnableRateLimiting("login-policy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            AppUser? user = await _userManager.FindByNameAsync(request.UserNameOrEmail);

            if (user == null)
                user = await _userManager.FindByEmailAsync(request.UserNameOrEmail);

            if (user == null)
                return Unauthorized(new { message = "Invalid username/email or password" });

            var checkPwd = await _userManager.CheckPasswordAsync(user, request.Password);

            if (!checkPwd)
                return Unauthorized(new { message = "Invalid username/email or password" });

            var roles = await _userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault() ?? "Student";

            var token = _jwtTokenService.GenerateToken(user, roleName);

            var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"] ?? "60");
            var expiration = DateTime.UtcNow.AddMinutes(expiresMinutes);

            return Ok(new
            {
                token,
                expiration,
                userName = user.UserName,
                fullName = user.FullName,
                role = roleName,
                avatarUrl = user.AvatarUrl
            });
        }

        // ==========================
        //  LẤY THÔNG TIN CÁ NHÂN
        // ==========================
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "No user id in token" });

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return Unauthorized(new { message = "User not found" });

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                fullName = user.FullName,
                email = user.Email,
                avatarUrl = user.AvatarUrl,
                createdAt = user.CreatedAt
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "No user id in token" });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            // ======================
            //  ĐỔI USERNAME
            // ======================
            if (!string.IsNullOrWhiteSpace(request.UserName) &&
                !string.Equals(request.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
            {
                var usernameExist = await _userManager.FindByNameAsync(request.UserName);
                if (usernameExist != null && usernameExist.Id != user.Id)
                {
                    return BadRequest(new { message = "Username already exists" });
                }
                user.UserName = request.UserName;
            }

            // ======================
            //  ĐỔI EMAIL
            // ======================
            if (!string.IsNullOrWhiteSpace(request.Email) &&
                !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailExist = await _userManager.FindByEmailAsync(request.Email);
                if (emailExist != null && emailExist.Id != user.Id)
                {
                    return BadRequest(new { message = "Email already in use" });
                }
                user.Email = request.Email;
            }

            // ======================
            //  ĐỔI FULLNAME
            // ======================
            user.FullName = request.FullName;

            // ======================
            //  ĐỔI AVATAR (NẾU CÓ)
            // ======================
            if (request.Avatar != null && request.Avatar.Length > 0)
            {
                var avatarsFolder = Path.Combine(_env.ContentRootPath, "Avatars");
                Directory.CreateDirectory(avatarsFolder);

                var ext = Path.GetExtension(request.Avatar.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                var fileName = $"{user.Id}{ext}";
                var filePath = Path.Combine(avatarsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Avatar.CopyToAsync(stream);
                }

                user.AvatarUrl = $"/avatars/{fileName}";
            }

            // Lưu lại DB
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Update profile failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                fullName = user.FullName,
                email = user.Email,
                avatarUrl = user.AvatarUrl,
                createdAt = user.CreatedAt
            });
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();

            var result = new List<object>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    id = u.Id,
                    userName = u.UserName,
                    fullName = u.FullName,
                    email = u.Email,
                    roles = roles
                });
            }

            return Ok(result);
        }

        // ==========================
        //  XOÁ TÀI KHOẢN
        // ==========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Không cho tự xoá chính mình (cho an toàn)
            if (user.Id == currentAdminId)
            {
                return BadRequest(new { message = "You cannot delete your own account" });
            }

            // (Tuỳ bạn, nếu muốn: chặn xoá Admin khác, chỉ cho đổi role thay vì xoá
            // ở đây mình cho xoá luôn cho đơn giản)

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Delete user failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }

            return Ok(new { message = "User deleted successfully", userId = userId });
        }
    }
}
