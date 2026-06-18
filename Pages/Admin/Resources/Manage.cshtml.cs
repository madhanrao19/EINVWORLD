using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using EINVWORLD.Data;
using EINVWORLD.Helpers;
using EINVWORLD.Models.Public;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace EINVWORLD.Pages.Admin.Resources
{
    [Authorize(Roles = "Admin")]
    public class ManageModel : PageModel
    {
        private readonly WebsiteDbContext _context;

        public ManageModel(WebsiteDbContext context)
        {
            _context = context;
        }

        public List<ResourceItem> Resources { get; set; } = new();
        public List<SelectListItem> ResourceTypeCodeOptions { get; set; } = new(); 


        [BindProperty(SupportsGet = true)] public string? FilterStatus { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterTitle { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterType { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? FilterDateFrom { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? FilterDateTo { get; set; }


        public void OnGet()
        {
            ResourceTypeCodeOptions = _context.ResourceTypes
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Code,
                    Text = r.Name
                }).ToList();

            var query = _context.Resources
                 .Include(r => r.ResourceType)
                 .AsQueryable();

            if (!string.IsNullOrEmpty(FilterStatus) && Enum.TryParse<ResourceStatus>(FilterStatus, true, out var parsed))
            {
                query = query.Where(r => r.Status == parsed);
            }

            if (!string.IsNullOrEmpty(FilterTitle))
                query = query.Where(r => r.Title.Contains(FilterTitle));

            if (!string.IsNullOrWhiteSpace(FilterType))
                query = query.Where(r => r.ResourceTypeCode == FilterType);

            if (FilterDateFrom.HasValue && FilterDateTo.HasValue)
            {
                var from = FilterDateFrom.Value.Date;
                var to = FilterDateTo.Value.Date;

                if (from > to)
                {
                    // Swap if user selected wrong range
                    (from, to) = (to, from);
                }

                query = query.Where(r => r.DatePublished.Date >= from && r.DatePublished.Date <= to);
            }
            else if (FilterDateFrom.HasValue)
            {
                var from = FilterDateFrom.Value.Date;
                query = query.Where(r => r.DatePublished.Date >= from);
            }
            else if (FilterDateTo.HasValue)
            {
                var to = FilterDateTo.Value.Date;
                query = query.Where(r => r.DatePublished.Date <= to);
            }


            Resources = query
                .OrderByDescending(r => r.DatePublished)
                .ToList();
        }



        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var resource = await _context.Resources.FindAsync(id);
            if (resource == null)
                return NotFound();

            // Delete image file
            if (!string.IsNullOrEmpty(resource.ImageUrl))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", resource.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (global::System.IO.File.Exists(imagePath))
                    global::System.IO.File.Delete(imagePath);
            }

            // Delete thumbnail file
            if (!string.IsNullOrEmpty(resource.ThumbnailUrl))
            {
                var thumbPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", resource.ThumbnailUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (global::System.IO.File.Exists(thumbPath))
                    global::System.IO.File.Delete(thumbPath);
            }

            _context.Resources.Remove(resource);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostBackfillSlugsAsync()
        {
            var resources = _context.Resources
                .Where(r => string.IsNullOrWhiteSpace(r.Slug))
                .ToList();

            int updated = 0;

            foreach (var r in resources)
            {
                var baseSlug = SlugHelper.GenerateSlug(r.Title, 190);
                var slug = baseSlug;
                int suffix = 1;

                while (_context.Resources.Any(x => x.Slug == slug && x.Id != r.Id))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                r.Slug = slug;
                updated++;
            }

            if (updated > 0)
                await _context.SaveChangesAsync();

            TempData["Message"] = $"✅ Backfilled {updated} missing slugs.";
            return RedirectToPage();
        }

        [BindProperty]
        public ResourceType NewType { get; set; } = new();

        public async Task<IActionResult> OnPostAddTypeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewType.Code) || string.IsNullOrWhiteSpace(NewType.Name))
            {
                TempData["Message"] = "❌ Type Code and Name are required.";
                return RedirectToPage();
            }

            if (await _context.ResourceTypes.AnyAsync(rt => rt.Code == NewType.Code))
            {
                TempData["Message"] = "❌ Type Code already exists.";
                return RedirectToPage();
            }

            _context.ResourceTypes.Add(NewType);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"✅ Resource Type '{NewType.Name}' added.";
            return RedirectToPage();
        }



    }
}
