using System;

namespace eInvWorld.Models.Audit
{
    public class UserActivityLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;  // From Identity
        public string UserName { get; set; } = null!; // Optional for quick view
        public string Action { get; set; } = null!;   // e.g. "Login", "TokenRequest", "ViewInvoice"
        public string? Module { get; set; }           // Optional: e.g. "Invoices", "Dashboard"
        public string? Data { get; set; }             // Optional JSON or string describing the action
        public string IpAddress { get; set; } = null!;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
