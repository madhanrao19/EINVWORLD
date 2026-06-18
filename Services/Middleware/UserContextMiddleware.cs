using Serilog.Context;

namespace eInvWorld.Services.Middleware
{
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();

            // ✅ GET USERNAME (Returns null if not logged in)
            var username = context.User?.Identity?.Name ?? "Anonymous";

            // ✅ PUSH BOTH PROPERTIES
            using (LogContext.PushProperty("IPAddress", ipAddress))
            using (LogContext.PushProperty("UserName", username))
            {
                await _next(context);
            }
        }
    }
}