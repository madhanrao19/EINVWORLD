using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.Recurring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.RecurringInvoices
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<RecurringProfile> Profiles { get; set; } = new List<RecurringProfile>();

        // Data properties for the summary cards
        public int TotalProfiles { get; set; }
        public int ActiveProfiles { get; set; }
        public int PausedProfiles { get; set; }

        // 1. Change 'Task' to 'Task<IActionResult>'
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Fetch all profiles belonging to the current user
            Profiles = await _context.RecurringProfiles
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Supplier)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Customer)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.PublicCustomer)
                .Where(p => p.CreatedByUserId == userId)
                .OrderBy(p => p.NextRunDate)
                .ToListAsync();

            // Calculate metrics for the UI cards
            TotalProfiles = Profiles.Count;
            ActiveProfiles = Profiles.Count(p => p.Status == "Active");
            PausedProfiles = Profiles.Count(p => p.Status == "Paused");

            var userCompany = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync();

            // Added a null check here (userCompany != null) to prevent a NullReferenceException 
            // just in case FirstOrDefaultAsync() doesn't find a record!
            if (userCompany == null || !userCompany.HasCompanyAccess || userCompany.IsViewOnly)
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            // 2. Add this at the end to render the normal HTML page if they pass the check
            return Page();
        }

        // Action handler for the Play/Pause button
        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.RecurringProfiles
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

            if (profile != null)
            {
                profile.Status = profile.Status == "Active" ? "Paused" : "Active";
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // Action handler for the Delete button
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.RecurringProfiles
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

            if (profile != null)
            {
                _context.RecurringProfiles.Remove(profile);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}