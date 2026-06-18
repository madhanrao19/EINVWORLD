using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EINVWORLD.Data;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Pages.Admin.Resources.Types
{
    public class DetailsModel : PageModel
    {
        private readonly EINVWORLD.Data.WebsiteDbContext _context;

        public DetailsModel(EINVWORLD.Data.WebsiteDbContext context)
        {
            _context = context;
        }

        public ResourceType ResourceType { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resourcetype = await _context.ResourceTypes.FirstOrDefaultAsync(m => m.Code == id);
            if (resourcetype == null)
            {
                return NotFound();
            }
            else
            {
                ResourceType = resourcetype;
            }
            return Page();
        }
    }
}
