using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EINVWORLD.Data;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Pages.Admin.Resources.Types
{
    public class EditModel : PageModel
    {
        private readonly EINVWORLD.Data.WebsiteDbContext _context;

        public EditModel(EINVWORLD.Data.WebsiteDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ResourceType ResourceType { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var resourcetype =  await _context.ResourceTypes.FirstOrDefaultAsync(m => m.Code == id);
            if (resourcetype == null)
            {
                return NotFound();
            }
            ResourceType = resourcetype;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ResourceType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ResourceTypeExists(ResourceType.Code))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ResourceTypeExists(string id)
        {
            return _context.ResourceTypes.Any(e => e.Code == id);
        }
    }
}
