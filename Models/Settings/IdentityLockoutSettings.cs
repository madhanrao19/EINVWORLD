namespace eInvWorld.Models.Settings
{
    public class IdentityLockoutSettings
    {
        public bool AllowedForNewUsers { get; set; }
        public int DefaultLockoutMinutes { get; set; }
        public int MaxFailedAccessAttempts { get; set; }
    }
}
