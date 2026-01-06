using ClassmateQRapi.Entities;
using Microsoft.AspNetCore.Identity;

namespace ClassmateQRapi.Entities
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            string[] roles = new[] { "Admin", "Teacher", "Student" };

            // Seed Roles
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // ✅ Seed tài khoản Admin mặc định
            var adminUserName = "admin";
            var adminEmail = "admin@gmail.com";
            var adminPassword = "Admin123@"; // nhớ đổi sau nếu cần

            var admin = await userManager.FindByNameAsync(adminUserName);
            if (admin == null)
            {
                admin = new AppUser
                {
                    UserName = adminUserName,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(admin, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
                else
                {
                    // Nếu muốn debug lỗi seed, bạn có thể log ra console:
                    Console.WriteLine("Failed to create default admin user:");
                    foreach (var err in createResult.Errors)
                    {
                        Console.WriteLine($" - {err.Code}: {err.Description}");
                    }
                }
            }
            else
            {
                // Đảm bảo user này có role Admin
                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }
        }
    }
}
