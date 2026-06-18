using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Keeps the image secure; only logged-in users can load it
    public class ImageController : ControllerBase
    {
        [HttpGet("logo")]
        public IActionResult GetCompanyLogo([FromQuery] string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("Filename is missing.");

            // Hardcode the base path here so users can't request arbitrary files from your server
            string basePath = @"E:\EINVWORLD\Documents\Companies\Logos";

            // Securely combine the path to prevent directory traversal attacks
            string fullPath = Path.GetFullPath(Path.Combine(basePath, fileName));

            if (!fullPath.StartsWith(basePath) || !System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            // Determine the mime type
            var ext = Path.GetExtension(fullPath).ToLower();
            var mimeType = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";

            // PhysicalFile streams the file efficiently without loading it all into memory
            return PhysicalFile(fullPath, mimeType);
        }
    }
}