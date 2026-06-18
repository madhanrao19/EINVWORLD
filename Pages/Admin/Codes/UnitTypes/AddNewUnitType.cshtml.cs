using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Admin.Codes.UnitTypes
{
    public class AddNewUnitTypeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddNewUnitTypeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public UnitType UnitType { get; set; } = null!;

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

            // Add the new unit type to the database
            _context.UnitTypes.Add(UnitType);
            _context.SaveChanges();

            return RedirectToPage("/Admin/Codes/UnitTypes/ListUnitType");
        }
    }
}
