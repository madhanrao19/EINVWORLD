namespace eInvWorld.Models
{
    public class CloudflareTurnstileResponse
    {
        public bool Success { get; set; }
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }
}
