using eInvWorld.Data;
using eInvWorld.Models.Audit;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace eInvWorld.Helpers
{
    public static class UserActivityLogger
    {
        /// <summary>
        /// Logs actions performed by a user via the web interface.
        /// </summary>
        public static async Task LogAsync(ApplicationDbContext db, HttpContext context, string action, string? module = null, string? data = null)
        {
            var user = context.User.Identity?.Name ?? "Unknown";
            var userId = context.User.FindFirst("sub")?.Value
                         ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                         ?? "Unknown";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var log = new UserActivityLog
            {
                UserId = userId,
                UserName = user,
                Action = action,
                Module = module,
                Data = data,
                IpAddress = ip,
                Timestamp = DateTime.UtcNow
            };

            db.UserActivityLogs.Add(log);
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Logs actions performed by internal system processes (Background Services).
        /// Used when HttpContext is unavailable.
        /// </summary>
        public static async Task LogSystemAsync(ApplicationDbContext db, string action, string? module = null, string? data = null)
        {
            var log = new UserActivityLog
            {
                UserId = "SYSTEM",
                UserName = "System Service",
                Action = action,
                Module = module,
                Data = data,
                IpAddress = "127.0.0.1", // Localhost for internal services
                Timestamp = DateTime.UtcNow
            };

            db.UserActivityLogs.Add(log);
            await db.SaveChangesAsync();
        }
    }
}