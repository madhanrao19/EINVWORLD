using System.IO;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Company logo uploads: PNG or JPEG only, decided by the file's first bytes (never its name or
    /// Content-Type). Logos are served anonymously from the app's origin, so an SVG or HTML file saved as a
    /// "logo" would run script there.
    /// </summary>
    public static class LogoImage
    {
        /// <summary>Returns ".png" or ".jpg" for a real PNG/JPEG stream, otherwise null. Rewinds the stream.</summary>
        public static string? DetectExtension(Stream stream)
        {
            var head = new byte[4];
            int read = stream.Read(head, 0, head.Length);
            if (stream.CanSeek) stream.Position = 0;
            if (read >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) return ".png";
            if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return ".jpg";
            return null;
        }
    }
}
