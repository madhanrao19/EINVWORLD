using EINVWORLD.Data; // Make sure this is your correct DbContext namespace
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Pages.Resources
{
    public class IndexModel : PageModel
	{
		private readonly WebsiteDbContext _context;

		public IndexModel(WebsiteDbContext context)
		{
			_context = context;
		}

		public List<ResourceItem> Resources { get; set; } = new();

        public void OnGet()
        {
            var query = _context.Resources
                .OrderByDescending(r => r.DatePublished)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(r => r.Status == ResourceStatus.Published);
            }

            Resources = query.ToList();
        }

    }
}
