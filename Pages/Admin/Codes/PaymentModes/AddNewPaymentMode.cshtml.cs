using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Admin.Codes.PaymentModes
{
    public class AddNewPaymentModeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddNewPaymentModeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PaymentMode PaymentMode { get; set; } = null!;

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

            // Add new Payment Mode to the database
            _context.PaymentMethods.Add(PaymentMode);
            _context.SaveChanges();

            return RedirectToPage("/Admin/Codes/PaymentModes/ListPaymentMode");
        }
    }
}
