using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using eInvWorld.Models;
using EINVWORLD.Helpers;
using System.IO;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Keeps the image secure; only logged-in users can load it
    public class ImageController : ControllerBase
    {
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

        private readonly FilePathConfig _filePathConfig;

        public ImageController(IOptions<FilePathConfig> filePathConfig)
        {
            _filePathConfig = filePathConfig.Value;
        }

        [HttpGet("logo")]
        public IActionResult GetCompanyLogo([FromQuery] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("Filename is missing.");

            // Config-driven base path (servers vary: D:\, E:\, custom install layout) and a
            // canonical traversal guard via SafePath instead of a hardcoded path + StartsWith check.
            if (!SafePath.TryResolve(_filePathConfig.CompanyLogosFolder, out var fullPath, fileName))
                return NotFound();

            var ext = Path.GetExtension(fullPath);
            if (!AllowedExtensions.Contains(ext))
                return NotFound();

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var mimeType = (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                ? "image/jpeg"
                : "image/png";

            // PhysicalFile streams the file efficiently without loading it all into memory
            return PhysicalFile(fullPath, mimeType);
        }
    }
}
