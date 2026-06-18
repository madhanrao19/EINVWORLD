using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using EINVWORLD.Data;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Pages.Admin.Resources.Types
{
    public class CreateModel : PageModel
    {
        private readonly EINVWORLD.Data.WebsiteDbContext _context;

        public CreateModel(EINVWORLD.Data.WebsiteDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public ResourceType ResourceType { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.ResourceTypes.Add(ResourceType);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
