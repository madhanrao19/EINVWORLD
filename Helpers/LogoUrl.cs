namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Browser URL for a company logo. Logos are stored outside wwwroot and PartyInfo.LogoPath holds the
    /// server file path (e.g. E:\...\Logos\x.png), which a browser cannot load — those go through the
    /// authorised /api/Image/logo endpoint. Web paths (~/..., /...) and an empty value (placeholder) pass
    /// through. PDF templates keep using the file path: the PDF engine reads local files.
    /// </summary>
    public static class LogoUrl
    {
        public const string Placeholder = "~/assets/images/users/multi-user.jpg";

        /// <summary>Returns an app-relative URL (pass through Url.Content).</summary>
        public static string For(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath)) return Placeholder;
            if (logoPath.StartsWith("~/") || logoPath.StartsWith("/") || logoPath.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                return logoPath;

            // Split on both separators: the stored path is Windows-style even when tests run on Linux.
            var fileName = logoPath.Split('\\', '/')[^1];
            return "~/api/Image/logo?fileName=" + System.Uri.EscapeDataString(fileName);
        }
    }
}
