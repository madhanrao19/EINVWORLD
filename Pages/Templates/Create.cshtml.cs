using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using eInvWorld.Data;
using eInvWorld.Models.Templates;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Templates
{
    [Authorize(Roles = "Admin,Supplier")]
    public class CreateModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public CreateModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public InvoiceTemplate InvoiceTemplate { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.InvoiceTemplates.Add(InvoiceTemplate);
            await _context.SaveChangesAsync();

            return RedirectToPage("./TemplateLists");
        }
    }
}
