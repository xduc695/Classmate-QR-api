using System.Security.Claims;

namespace ClassMate.Api.Middlewares
{
    public class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLogMiddleware> _logger;

        public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var start = DateTime.UtcNow;

            await _next(context);

            var duration = DateTime.UtcNow - start;

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var path = context.Request.Path.Value ?? "";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            _logger.LogInformation(
                "AUDIT | User:{UserId} | IP:{IP} | {Method} {Path} | Status:{Status} | {Duration} ms",
                userId,
                ip,
                method,
                path,
                statusCode,
                duration.TotalMilliseconds
            );
        }
    }
}
