using System;

namespace eInvWorld.Models.Audit
{
    public class LHDNTokenLog
    {
        public int Id { get; set; }
        public string TIN { get; set; } = null!;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string ClientIdUsed { get; set; } = null!;
        public string Source { get; set; } = "System"; // "Background", "UserRequest", etc.
    }

}
