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

        /// <summary>Size cap for an admin resource (article/editor) image.</summary>
        public const long MaxResourceImageBytes = 5 * 1024 * 1024;

        /// <summary>
        /// Message to show, or null when the upload is a real PNG/JPEG no larger than <paramref name="maxBytes"/>.
        /// Checked before ImageMagick decodes anything, and so no SVG/HTML is ever stored.
        /// </summary>
        public static string? Reject(Microsoft.AspNetCore.Http.IFormFile file, long maxBytes)
        {
            if (file.Length == 0) return "No image uploaded.";
            if (file.Length > maxBytes) return $"Image must be {maxBytes / (1024 * 1024)} MB or smaller.";
            using var stream = file.OpenReadStream();
            return DetectExtension(stream) == null ? "Only PNG or JPEG images are allowed." : null;
        }
    }
}
