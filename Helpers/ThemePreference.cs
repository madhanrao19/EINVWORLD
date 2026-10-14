using Microsoft.AspNetCore.Http;

namespace eInvWorld.Helpers
{
    /// <summary>
    /// The user's saved light/dark choice (the <c>einv-theme</c> cookie written by the topbar toggle),
    /// allowlisted before it goes anywhere near HTML. Null means "no saved choice — follow the OS".
    /// Used by both Tabler layouts (server-rendered <c>data-bs-theme</c>), the favicon/theme-color
    /// partial and the Turnstile widget, so they always agree.
    /// </summary>
    public static class ThemePreference
    {
        public const string CookieName = "einv-theme";

        public static string? Read(HttpContext? context)
        {
            var value = context?.Request.Cookies[CookieName];
            return value == "dark" || value == "light" ? value : null;
        }
    }
}
