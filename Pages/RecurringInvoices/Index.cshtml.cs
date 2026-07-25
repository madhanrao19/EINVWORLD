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

        // Latest run per profile (ProfileId -> most recent history row), for the "Last Run" column.
        public Dictionary<int, RecurringRunHistory> LastRunByProfile { get; set; } = new();

        // Data properties for the summary cards
        public int TotalProfiles { get; set; }
        public int ActiveProfiles { get; set; }
        public int PausedProfiles { get; set; }
        public int DueNext7DaysCount { get; set; }
        public int FailedRunsLast30DaysCount { get; set; }
        public int GeneratedThisMonthCount { get; set; }

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
            var sevenDaysOut = DateTime.Today.AddDays(7);
            DueNext7DaysCount = Profiles.Count(p => p.Status == "Active" && p.NextRunDate.Date <= sevenDaysOut);

            var profileIds = Profiles.Select(p => p.Id).ToList();
            if (profileIds.Count > 0)
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                var runHistories = await _context.RecurringRunHistories
                    .Where(h => profileIds.Contains(h.RecurringProfileId))
                    .ToListAsync();

                LastRunByProfile = runHistories
                    .GroupBy(h => h.RecurringProfileId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.RunTimestamp).First());

                FailedRunsLast30DaysCount = runHistories.Count(h =>
                    h.RunTimestamp >= thirtyDaysAgo && (h.RunStatus.StartsWith("Failed") || h.RunStatus == "LHDN_Failed"));

                GeneratedThisMonthCount = runHistories.Count(h =>
                    h.RunTimestamp >= monthStart && h.RunStatus.StartsWith("Success"));
            }

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