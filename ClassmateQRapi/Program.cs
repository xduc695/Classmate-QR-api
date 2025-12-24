using ClassMate.Api.Middlewares;
using ClassmateQRapi.Data;
using ClassmateQRapi.Entities;
using ClassmateQRapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()   // Cho phép mọi nguồn (Web, Mobile...)
                   .AllowAnyMethod()   // Cho phép GET, POST, PUT, DELETE...
                   .AllowAnyHeader();  // Cho phép mọi Header
        });
});


// 1. DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT config
var jwtSection = builder.Configuration.GetSection("Jwt");
Console.WriteLine("JWT KEY AT STARTUP: " + jwtSection["Key"]);
var keyBytes = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.IncludeErrorDetails = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],

            // *** BẬT kiểm tra hết hạn token ***
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                Console.WriteLine("AUTH HEADER     : '" + authHeader + "'");
                Console.WriteLine("AUTH FAILED TYPE: " + context.Exception.GetType().Name);
                Console.WriteLine("AUTH FAILED MSG : " + context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 4. JwtTokenService
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// 5. Rate Limiter: giới hạn /login
builder.Services.AddRateLimiter(options =>
{
    // Ví dụ: tối đa 5 lần login / 1 phút / mỗi IP
    options.AddPolicy("login-policy", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 5,
                TokensPerPeriod = 5,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// 6. MVC Controllers
builder.Services.AddControllers();

// 7. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ClassMate API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// 8. Serve static Avatars
// --- Đặt đoạn này TRƯỚC app.UseAuthentication() ---

var avatarPath = Path.Combine(builder.Environment.ContentRootPath, "Avatars");
var assignmentsPath = Path.Combine(builder.Environment.ContentRootPath, "assignments");
var submissionsPath = Path.Combine(builder.Environment.ContentRootPath, "Submissions");

// Đảm bảo thư mục tồn tại
Directory.CreateDirectory(avatarPath);
Directory.CreateDirectory(assignmentsPath);
Directory.CreateDirectory(submissionsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarPath),
    RequestPath = "/avatars"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(assignmentsPath),
    RequestPath = "/assignments"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(submissionsPath),
    RequestPath = "/Submissions"
});

// 9. Rate limiter
app.UseRateLimiter();

// 10. Auth + Audit Log
app.UseAuthentication();
app.UseMiddleware<AuditLogMiddleware>();   // log sau khi xác thực sẽ có userId
app.UseAuthorization();
app.UseStaticFiles(); // Cho phép truy cập file tĩnh (Upload)

app.MapControllers();

await DbSeeder.SeedAsync(app);
app.Run();