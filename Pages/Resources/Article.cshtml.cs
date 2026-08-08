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

        /// <summary>Effective &lt;title&gt;/meta-description text: falls back to Title/Summary when the
        /// SEO fields (MetaTitle/MetaDescription) are blank. Consumed by _HomeLayout's ViewData["Title"]
        /// / ViewData["Description"] — see Article.cshtml.</summary>
        public string SeoTitle => !string.IsNullOrWhiteSpace(Article?.MetaTitle) ? Article!.MetaTitle! : Article?.Title ?? "";
        public string SeoDescription => !string.IsNullOrWhiteSpace(Article?.MetaDescription) ? Article!.MetaDescription! : Article?.Summary ?? "";

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
