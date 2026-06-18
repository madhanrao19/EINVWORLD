using System;
namespace eInvWorld.Models.Auth
{
    public class LHDNToken
    {
        public int Id { get; set; } // ✅ Primary Key
        public string TIN { get; set; } = null!; // Keyed by client TIN for intermediary
        public string AccessToken { get; set; } = null!;
        public DateTime ExpiryTime { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
