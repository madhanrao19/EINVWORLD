using EINVWORLD.Data;
using EINVWORLD.Models.Public;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EINVWORLD.Pages.Resources
{
    public class ArticleModel : PageModel
    {
        private readonly WebsiteDbContext _context;

        public ArticleModel(WebsiteDbContext context)
        {
            _context = context;
        }

        public ResourceItem? Article { get; set; }

        public IActionResult OnGet(string slug)
        {
            Article = _context.Resources.FirstOrDefault(r => r.Slug == slug);

            if (Article == null)
            {
                return NotFound();
            }

            return Page();
        }

    }

}
