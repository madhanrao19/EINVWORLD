namespace eInvWorld.Models.Settings
{
    public class SessionSettings
    {
        public int IdleTimeoutMinutes { get; set; }
        public string CookieName { get; set; } = null!;
    }
}
