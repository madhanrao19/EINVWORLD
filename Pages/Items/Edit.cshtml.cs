using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Items
{
    [Authorize(Roles = "Admin,Supplier")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly DropdownHelper _dropdownHelper;

        public EditModel(ApplicationDbContext context, DropdownHelper dropdownHelper)
        {
            _context = context;
            _dropdownHelper = dropdownHelper;
        }

        [BindProperty]
        public ItemDescription ItemDescription { get; set; } = default!;

        public List<SelectListItem> ClassificationCodes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var itemdescription = await _context.ItemDescriptions.FirstOrDefaultAsync(m => m.Id == id);
            if (itemdescription == null) return NotFound();

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
            ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
                return Page();
            }

            var existingItem = await _context.ItemDescriptions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == ItemDescription.Id);
            if (existingItem != null)
            {
                ItemDescription.CreatedByCompanyId = existingItem.CreatedByCompanyId;
            }

            // Get Malaysia Time & Username
            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
            ItemDescription.UpdatedDate = malaysiaTime;
            ItemDescription.UpdatedBy = User.Identity?.Name ?? "System";

            _context.Attach(ItemDescription).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}