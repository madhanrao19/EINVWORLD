using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Admin.Codes.TaxTypes
{
    public class AddNewTaxTypeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddNewTaxTypeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TaxType TaxType { get; set; } = null!;

        public void OnGet()
        {
            // Display the form
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Add the new tax type to the database
            _context.TaxTypes.Add(TaxType);
            _context.SaveChanges();

            return RedirectToPage("/Admin/Codes/TaxTypes/ListTaxType");
        }
    }
}
