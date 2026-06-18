using System.Security.Claims;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.PublicCustomer
{
    [Authorize(Roles = "Admin,Supplier")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public eInvWorld.Models.InputModel.PublicCustomer PublicCustomer { get; set; } = default!;

        public bool CanEdit { get; set; }


        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
                return NotFound();

            var entity = await _context.PublicCustomers
                .Include(p => p.State)
                .Include(p => p.Country)
                .Include(p => p.RegType)
                .FirstOrDefaultAsync(p => p.PublicCustomerId == id);


            if (entity == null)
                return NotFound();

            // 🔐 Admin can view all
            if (User.IsInRole("Admin"))
            {
                PublicCustomer = entity;
                return Page();
            }

            // 🔐 Supplier restriction
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userCompany = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync();

            if (userCompany == null)
                return Redirect("/Identity/Account/AccessDenied");

            if (entity.CreatedByCompanyId != userCompany.PartyInfoId)
                return Redirect("/Identity/Account/AccessDenied");

            if (!userCompany.HasCompanyAccess || userCompany.IsViewOnly)
            {
                return Redirect("/Identity/Account/AccessDenied");
            }
            PublicCustomer = entity;
            // Admin can always edit
            if (User.IsInRole("Admin"))
            {
                CanEdit = true;
            }
            else
            {

                CanEdit = userCompany != null &&
                          entity.CreatedByCompanyId == userCompany.PartyInfoId;
            }

            return Page();
        }

    }
}
