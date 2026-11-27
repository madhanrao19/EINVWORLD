namespace eInvWorld.Models.Settings
{
    public class SessionSettings
    {
        public int IdleTimeoutMinutes { get; set; }
        public string CookieName { get; set; } = null!;

        /// <summary>
        /// Sliding lifetime, in days, of a sign-in made with "Remember me" ticked. 0 (or less) keeps such a sign-in on
        /// <see cref="IdleTimeoutMinutes"/> like any other. Revocation still works through the security stamp check.
        /// </summary>
        public int RememberMeDays { get; set; } = 14;

        /// <summary>
        /// The auth ticket expiry for a sign-in, or null to keep the cookie handler's default (IdleTimeoutMinutes).
        /// </summary>
        public DateTimeOffset? RememberMeExpiry(bool isPersistent, DateTimeOffset nowUtc) =>
            isPersistent && RememberMeDays > 0 ? nowUtc.AddDays(RememberMeDays) : null;
    }
}
