using EINVWORLD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ImageMagick;
using EINVWORLD.Helpers;
using EINVWORLD.Models.Public;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using eInvWorld.Models;

namespace EINVWORLD.Pages.Admin.Resources
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly WebsiteDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly FilePathConfig _filePathConfig;

        public EditModel(WebsiteDbContext context, IWebHostEnvironment env, IOptions<FilePathConfig> filePathConfig)
        {
            _context = context;
            _env = env;

            _filePathConfig = filePathConfig.Value;
        }

        [BindProperty]
        public ResourceItem Resource { get; set; } = default!;

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        public List<SelectListItem> TypeOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Resource = (await _context.Resources
                .Include(r => r.ResourceType)
                .FirstOrDefaultAsync(r => r.Id == id))!;

            if (Resource == null)
                return NotFound();

            LoadTypeOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LoadTypeOptions();

            var existing = await _context.Resources.FindAsync(Resource.Id);
            if (existing == null)
                return NotFound();

            existing.Title = Resource.Title;
            existing.Summary = Resource.Summary;
            existing.ResourceTypeCode = Resource.ResourceTypeCode;
            existing.ContentHtml = Resource.ContentHtml;
            existing.Status = Resource.Status;
            existing.DatePublished = Resource.DatePublished == default ? DateTime.Now : Resource.DatePublished;

            if (ImageFile != null)
            {
                DeleteFile(existing.ImageUrl);
                DeleteFile(existing.ThumbnailUrl);

                var typeName = _context.ResourceTypes
                    .Where(t => t.Code == existing.ResourceTypeCode)
                    .Select(t => t.Name)
                    .FirstOrDefault() ?? "general";

                var safeFolder = SlugHelper.GenerateSlug(typeName);

                // Use external path from configuration instead of _env.WebRootPath
                var baseFolder = Path.Combine(_filePathConfig.ResourceImagesFolder, safeFolder);
                var fullPath = Path.Combine(baseFolder, "full");
                var thumbPath = Path.Combine(baseFolder, "thumb");

                Directory.CreateDirectory(fullPath);
                Directory.CreateDirectory(thumbPath);

                var fileName = Guid.NewGuid() + ".webp";
                var fullImagePath = Path.Combine(fullPath, fileName);
                var thumbImagePath = Path.Combine(thumbPath, fileName);

                using (var fullImage = new MagickImage(ImageFile.OpenReadStream()))
                {
                    fullImage.Format = MagickFormat.WebP;
                    fullImage.Quality = 85;
                    await fullImage.WriteAsync(fullImagePath);
                }

                using (var thumbImage = new MagickImage(ImageFile.OpenReadStream()))
                {
                    // Resize to cover then crop to an exact 310x207 (equivalent to ImageSharp ResizeMode.Crop)
                    thumbImage.Resize(new MagickGeometry(310, 207) { FillArea = true });
                    thumbImage.Crop(310, 207, Gravity.Center);
                    thumbImage.ResetPage();
                    thumbImage.Format = MagickFormat.WebP;
                    thumbImage.Quality = 75;

                    await thumbImage.WriteAsync(thumbImagePath);
                }

                // Save the API URL instead of the static webroot URL
                existing.ImageUrl = $"/api/resources/images/{safeFolder}/full/{fileName}";
                existing.ThumbnailUrl = $"/api/resources/images/{safeFolder}/thumb/{fileName}";
            }

            if (string.IsNullOrWhiteSpace(Resource.Slug))
            {
                var baseSlug = SlugHelper.GenerateSlug(existing.Title, 190);
                var slug = baseSlug;
                int suffix = 1;

                while (_context.Resources.Any(r => r.Slug == slug && r.Id != existing.Id))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                existing.Slug = slug;
            }
            else
            {
                var sanitized = SlugHelper.GenerateSlug(Resource.Slug, 190);
                var slug = sanitized;
                int suffix = 1;

                while (_context.Resources.Any(r => r.Slug == slug && r.Id != existing.Id))
                {
                    slug = $"{sanitized}-{suffix++}";
                }

                existing.Slug = slug;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("/Admin/Resources/Manage");
        }

        private void LoadTypeOptions()
        {
            TypeOptions = _context.ResourceTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Code.ToString(),
                    Text = t.Name
                }).ToList();
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            string path = "";

            // If it's a new external API image
            if (url.StartsWith("/api/resources/images/"))
            {
                var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
                // parts will be: [0]api, [1]resources, [2]images, [3]{category}, [4]{size}, [5]{fileName}
                if (parts.Length >= 6)
                {
                    path = Path.Combine(_filePathConfig.ResourceImagesFolder, parts[3], parts[4], parts[5]);
                }
            }
            // If it's an old webroot image
            else if (url.StartsWith("/images/"))
            {
                path = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }

            if (!string.IsNullOrEmpty(path) && global::System.IO.File.Exists(path))
            {
                global::System.IO.File.Delete(path);
            }
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostUploadImageAsync(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { error = "No image uploaded" });

            // Use external path from configuration
            var folder = _filePathConfig.EditorUploadsFolder;
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var savePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(savePath, FileMode.Create);
            await image.CopyToAsync(stream);

            // Return the API URL to be served via ResourcesApiController
            return new JsonResult(new { location = $"/api/resources/editor/{fileName}" });
        }
    }
}
