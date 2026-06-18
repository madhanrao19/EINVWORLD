using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models.Templates;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Templates
{
    [Authorize(Roles = "Admin,Supplier")]
    public class EditModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public EditModel(eInvWorld.Data.ApplicationDbContext context)
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

            var invoicetemplate = await _context.InvoiceTemplates
                .Include(m => m.Supplier)
                .Include(m => m.Customer)
                .Include(m => m.PublicCustomer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoicetemplate == null)
            {
                return NotFound();
            }
            InvoiceTemplate = invoicetemplate;
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

            _context.Attach(InvoiceTemplate).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InvoiceTemplateExists(InvoiceTemplate.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./TemplateLists");
        }

        private bool InvoiceTemplateExists(int id)
        {
            return _context.InvoiceTemplates.Any(e => e.Id == id);
        }
    }
}
