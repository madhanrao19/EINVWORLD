using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Items
{
    [Authorize(Roles = "Admin,Supplier")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ItemDescription ItemDescription { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var itemdescription = await _context.ItemDescriptions.FirstOrDefaultAsync(m => m.Id == id);
            if (itemdescription == null) return NotFound();

            // Security check
            if (!User.IsInRole("Admin"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies.Where(uc => uc.UserId == userId).OrderByDescending(uc => uc.IsPrimaryCompany).FirstOrDefaultAsync();

                if (userCompany == null || itemdescription.CreatedByCompanyId != userCompany.PartyInfoId)
                {
                    return Redirect("/Identity/Account/AccessDenied");
                }
                if (!userCompany.HasCompanyAccess || userCompany.IsViewOnly)
                {
                    return Redirect("/Identity/Account/AccessDenied");
                }
            }

            ItemDescription = itemdescription;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null) return NotFound();

            var itemdescription = await _context.ItemDescriptions.FindAsync(id);

            if (itemdescription != null)
            {
                _context.ItemDescriptions.Remove(itemdescription);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}