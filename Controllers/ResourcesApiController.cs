using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using eInvWorld.Models;

namespace EINVWORLD.Controllers
{
    [Route("api")]
    [ApiController]
    [AllowAnonymous] // Allow public access to resource images
    public class ResourcesApiController : ControllerBase
    {
        private readonly FilePathConfig _filePathConfig;

        public ResourcesApiController(IOptions<FilePathConfig> filePathConfig)
        {
            _filePathConfig = filePathConfig.Value;
        }

        [HttpGet("resources/images/{category}/{size}/{fileName}")]
        public async Task<IActionResult> GetResourceImage(string category, string size, string fileName)
        {
            try
            {
                // Validate size parameter
                if (size != "full" && size != "thumb")
                {
                    return BadRequest("Invalid size parameter. Use 'full' or 'thumb'.");
                }

                // Build the file path, rejecting any path-traversal in the user-supplied segments
                if (!TryResolveSafePath(_filePathConfig.ResourceImagesFolder, out var filePath, category, size, fileName))
                {
                    return BadRequest("Invalid path.");
                }

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Image not found");
                }

                // Read the file
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                // Determine content type based on file extension
                var contentType = GetContentType(Path.GetExtension(fileName));

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error serving image: {ex.Message}");
            }
        }

        [HttpGet("resources/editor/{fileName}")]
        public async Task<IActionResult> GetEditorImage(string fileName)
        {
            try
            {
                // Build the file path, rejecting any path-traversal in the user-supplied file name
                if (!TryResolveSafePath(_filePathConfig.EditorUploadsFolder, out var filePath, fileName))
                {
                    return BadRequest("Invalid path.");
                }

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Editor image not found");
                }

                // Read the file
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                // Determine content type based on file extension
                var contentType = GetContentType(Path.GetExtension(fileName));

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error serving editor image: {ex.Message}");
            }
        }

        [HttpGet("companies/logos/{fileName}")]
        public async Task<IActionResult> GetCompanyLogo(string fileName)
        {
            try
            {
                // Build the file path, rejecting any path-traversal in the user-supplied file name
                if (!TryResolveSafePath(_filePathConfig.CompanyLogosFolder, out var filePath, fileName))
                {
                    return BadRequest("Invalid path.");
                }

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Company logo not found");
                }

                // Read the file
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                // Determine content type based on file extension
                var contentType = GetContentType(Path.GetExtension(fileName));

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error serving company logo: {ex.Message}");
            }
        }

        /// <summary>
        /// Combines <paramref name="baseFolder"/> with the user-supplied <paramref name="segments"/> and
        /// guarantees the resolved path stays inside the base folder. Rejects empty segments and any segment
        /// containing directory separators or traversal sequences (e.g. "..", "..%2f"), defeating path-traversal.
        /// </summary>
        private static bool TryResolveSafePath(string baseFolder, out string fullPath, params string[] segments)
        {
            fullPath = string.Empty;

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) ||
                    segment.Contains("..", StringComparison.Ordinal) ||
                    segment.IndexOf('/') >= 0 ||
                    segment.IndexOf('\\') >= 0 ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }
            }

            var baseFull = Path.GetFullPath(baseFolder);
            var basePrefix = baseFull.EndsWith(Path.DirectorySeparatorChar)
                ? baseFull
                : baseFull + Path.DirectorySeparatorChar;

            var combined = Path.GetFullPath(Path.Combine(new[] { baseFolder }.Concat(segments).ToArray()));

            // Final canonical check: the resolved path must live under the base folder.
            if (!combined.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = combined;
            return true;
        }

        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".bmp" => "image/bmp",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }
    }
}