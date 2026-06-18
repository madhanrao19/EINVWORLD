using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ImageMagick;
using EINVWORLD.Data;
using EINVWORLD.Helpers;
using EINVWORLD.Models.Public;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using eInvWorld.Models;
using System.Diagnostics;

namespace EINVWORLD.Pages.Admin.Resources
{
    public class CreateModel : PageModel
    {
        private readonly WebsiteDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly FilePathConfig _filePathConfig;

        public CreateModel(WebsiteDbContext context, IWebHostEnvironment env, IOptions<FilePathConfig> filePathConfig)
        {
            _context = context;
            _env = env;
            _filePathConfig = filePathConfig.Value;
        }

        [BindProperty]
        public ResourceItem Resource { get; set; } = new();

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        public List<SelectListItem> ResourceTypeCodeOptions { get; set; } = new();

        public void OnGet()
        {
            Resource = new ResourceItem { DatePublished = DateTime.Today };

            ResourceTypeCodeOptions = _context.ResourceTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Code,
                    Text = t.Name
                }).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ResourceTypeCodeOptions = _context.ResourceTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Code,
                    Text = t.Name
                }).ToList();

            var type = await _context.ResourceTypes.FirstOrDefaultAsync(t => t.Code == Resource.ResourceTypeCode);
            if (type != null)
            {
                Resource.ResourceType = type;
            }

            if (ImageFile != null)
            {
                var safeFolder = SlugHelper.GenerateSlug(type?.Name ?? "general");
                var fileName = Guid.NewGuid() + ".webp";
                
                // Use external path from configuration
                var baseFolder = Path.Combine(_filePathConfig.ResourceImagesFolder, safeFolder);
                var fullFolder = Path.Combine(baseFolder, "full");
                var thumbFolder = Path.Combine(baseFolder, "thumb");

                Directory.CreateDirectory(fullFolder);
                Directory.CreateDirectory(thumbFolder);

                var fullImagePath = Path.Combine(fullFolder, fileName);
                var thumbImagePath = Path.Combine(thumbFolder, fileName);

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

                // Store relative paths that will be served via API/handler
                Resource.ImageUrl = $"/api/resources/images/{safeFolder}/full/{fileName}";
                Resource.ThumbnailUrl = $"/api/resources/images/{safeFolder}/thumb/{fileName}";
            }

            if (string.IsNullOrWhiteSpace(Resource.ResourceTypeCode))
            {
                ModelState.AddModelError("Resource.ResourceTypeCode", "Please select a category.");
            }


            if (!ModelState.IsValid)
            {
                // Log validation errors
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        Debug.WriteLine($"❌ {entry.Key}: {error.ErrorMessage}");
                    }
                }
                return Page();
            }

            if (Resource.DatePublished == default)
            {
                Resource.DatePublished = DateTime.Now;
            }

            if (string.IsNullOrWhiteSpace(Resource.Slug))
            {
                var baseSlug = SlugHelper.GenerateSlug(Resource.Title, 190);
                var slug = baseSlug;
                int suffix = 1;
                while (_context.Resources.Any(r => r.Slug == slug))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }
                Resource.Slug = slug;
            }

            _context.Resources.Add(Resource);
            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ Resource created successfully.";
            return RedirectToPage("/Admin/Resources/Manage");
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

            return new JsonResult(new { location = $"/api/resources/editor/{fileName}" });
        }
    }
}
