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

            var (allowed, companyId) = await ManageScopeAsync(_context, User);
            if (!allowed || (companyId.HasValue && itemdescription.CreatedByCompanyId != companyId))
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            ItemDescription = itemdescription;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null) return NotFound();

            var itemdescription = await _context.ItemDescriptions.FindAsync(id);

            // The confirmation page checks ownership on GET; the POST must too, or any supplier could
            // delete another company's item by posting its id.
            var (allowed, companyId) = await ManageScopeAsync(_context, User);
            if (!allowed || (itemdescription != null && companyId.HasValue && itemdescription.CreatedByCompanyId != companyId))
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            if (itemdescription != null)
            {
                _context.ItemDescriptions.Remove(itemdescription);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }

        /// <summary>
        /// Who may change or delete items: an Admin may manage every item (CompanyId null); a supplier user
        /// with edit access to their primary company may manage that company's items; anyone else, none.
        /// Shared by the row Delete page and the Items list bulk actions.
        /// </summary>
        internal static async Task<(bool Allowed, int? CompanyId)> ManageScopeAsync(ApplicationDbContext context, ClaimsPrincipal user)
        {
            if (user.IsInRole("Admin")) return (true, null);

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompany = await context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync();

            if (userCompany == null || !userCompany.HasCompanyAccess || userCompany.IsViewOnly) return (false, null);
            return (true, userCompany.PartyInfoId);
        }
    }
}