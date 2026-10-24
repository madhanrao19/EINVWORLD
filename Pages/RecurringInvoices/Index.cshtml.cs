using eInvWorld.Data;
using EINVWORLD.Helpers;
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
                .Include(uc => uc.CompanyRole)
                .Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync();

            // A CompanyRole assignment (Owner/Admin/Editor/Viewer) always grants at least read
            // access; fall back to the legacy flags only for memberships never migrated to a role.
            bool hasAccess = userCompany != null &&
                (userCompany.CompanyRole != null || (userCompany.HasCompanyAccess && !userCompany.IsViewOnly));

            if (!hasAccess)
            {
                return Redirect("/Identity/Account/AccessDenied");
            }

            // 2. Add this at the end to render the normal HTML page if they pass the check
            return Page();
        }

        /// <summary>
        /// Write rule for recurring profiles, checked by every recurring POST and again by the worker at run time:
        /// the user must be a non-view-only member of the company that ISSUES the template's invoices (the customer
        /// for self-billed types 11–14) — the same rule as <c>UserExtensions.CanManageInvoiceAsync</c>. Without it a
        /// member later made view-only could still resume a profile or switch on auto-submit.
        /// </summary>
        internal static async Task<bool> CanWriteTemplateAsync(ApplicationDbContext context, string? userId, int templateId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            var t = await context.InvoiceTemplates
                .Where(x => x.Id == templateId)
                .Select(x => new { x.DocTypeCode, x.SupplierId, x.CustomerId })
                .FirstOrDefaultAsync();
            var issuerId = t == null ? null : TinHelper.IsSelfBilledDocType(t.DocTypeCode) ? t.CustomerId : t.SupplierId;
            // Like CanManageInvoiceAsync, a shared LHDN general-TIN party (General Public etc.) is never "owned".
            return issuerId != null
                && await context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == issuerId && !uc.IsViewOnly)
                && !GeneralTINHelper.IsGeneralTIN(await context.PartyInfos.Where(pi => pi.PartyInfoId == issuerId).Select(pi => pi.TIN).FirstOrDefaultAsync());
        }

        private async Task<bool> MayWriteAsync(string? userId, int templateId) =>
            User.IsInRole("Admin") || await CanWriteTemplateAsync(_context, userId, templateId);

        // Action handler for the Play/Pause button
        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.RecurringProfiles
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

            if (profile != null && !await MayWriteAsync(userId, profile.InvoiceTemplateId))
                return Redirect("/Identity/Account/AccessDenied");

            if (profile != null)
            {
                profile.Status = profile.Status == "Active" ? "Paused" : "Active";
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // Bulk bar: pause / resume / delete the selected profiles. Same ownership rule as the row actions.
        public async Task<IActionResult> OnPostBulkAsync(int[] ids, string bulkAction)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var selected = (ids ?? Array.Empty<int>()).Distinct().Take(500).ToList();
            var profiles = await _context.RecurringProfiles
                .Where(p => selected.Contains(p.Id) && p.CreatedByUserId == userId)
                .ToListAsync();

            // Skip any profile whose issuing company the user can't write to (view-only there, or no longer a member).
            var writable = new List<RecurringProfile>();
            foreach (var p in profiles)
                if (await MayWriteAsync(userId, p.InvoiceTemplateId)) writable.Add(p);
            profiles = writable;

            switch (bulkAction)
            {
                case "pause": profiles.ForEach(p => p.Status = "Paused"); break;
                case "resume": profiles.ForEach(p => p.Status = "Active"); break;
                case "delete": _context.RecurringProfiles.RemoveRange(profiles); break;
                default: return BadRequest();
            }

            await _context.SaveChangesAsync();
            return RedirectToPage();
        }

        // Action handler for the Delete button
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.RecurringProfiles
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

            if (profile != null && !await MayWriteAsync(userId, profile.InvoiceTemplateId))
                return Redirect("/Identity/Account/AccessDenied");

            if (profile != null)
            {
                _context.RecurringProfiles.Remove(profile);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}