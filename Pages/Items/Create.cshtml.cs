using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
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
    public class CreateModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly DropdownHelper _dropdownHelper;

        public CreateModel(ApplicationDbContext context, DropdownHelper dropdownHelper) : base(context)
        {
            _context = context;
            _dropdownHelper = dropdownHelper;
        }

        [BindProperty]
        public ItemDescription ItemDescription { get; set; } = new();

        public List<SelectListItem> ClassificationCodes { get; set; } = new();

        public IActionResult OnGet()
        {
            ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Repopulate on validation failure
                ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany != null)
                {
                    ItemDescription.CreatedByCompanyId = userCompany.PartyInfoId;
                }
                else
                {
                    ModelState.AddModelError("", "You are not assigned to any supplier company.");
                    ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
                    return Page();
                }
            }

            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
            ItemDescription.UpdatedDate = malaysiaTime;
            ItemDescription.UpdatedBy = User.Identity?.Name ?? "System";

            _context.ItemDescriptions.Add(ItemDescription);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}