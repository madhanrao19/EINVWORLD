using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models.Templates;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Templates
{
    [Authorize(Roles = "Admin,Supplier")]
    public class DeleteModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public DeleteModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InvoiceTemplate InvoiceTemplate { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoicetemplate = await _context.InvoiceTemplates.FirstOrDefaultAsync(m => m.Id == id);

            if (invoicetemplate == null)
            {
                return NotFound();
            }
            else
            {
                InvoiceTemplate = invoicetemplate;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoicetemplate = await _context.InvoiceTemplates.FindAsync(id);
            if (invoicetemplate != null)
            {
                InvoiceTemplate = invoicetemplate;
                _context.InvoiceTemplates.Remove(InvoiceTemplate);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./TemplateLists");
        }
    }
}
