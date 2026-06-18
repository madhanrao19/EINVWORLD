using eInvWorld.Data;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Recurring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.RecurringInvoices
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public RecurringProfile Input { get; set; } = default!;

        // Display properties for locked template data
        public string BaseTemplateName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public IList<RecurringRunHistory> RunHistories { get; set; } = new List<RecurringRunHistory>();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Fetch the profile and include template info for the read-only display
            var profile = await _context.RecurringProfiles
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Supplier)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.Customer)
                .Include(p => p.InvoiceTemplate)
                    .ThenInclude(t => t!.PublicCustomer)
                .FirstOrDefaultAsync(m => m.Id == id && m.CreatedByUserId == userId);

            if (profile == null)
            {
                TempData["StatusMessage"] = "Error: Profile not found or unauthorized.";
                return RedirectToPage("./Index");
            }

            Input = profile;

            // Populate the UI display fields
            BaseTemplateName = profile.InvoiceTemplate?.TemplateName ?? "Unknown Template";
            SupplierName = profile.InvoiceTemplate?.Supplier?.CompanyName ?? "Unknown Company";
            BuyerName = profile.InvoiceTemplate?.Customer?.CompanyName ?? profile.InvoiceTemplate?.PublicCustomer?.CompanyName ?? "All Buyers";

            RunHistories = await _context.RecurringRunHistories
                .Where(h => h.RecurringProfileId == id)
                .OrderByDescending(h => h.RunTimestamp)
                .Take(50) // Show the last 50 runs
                .ToListAsync();
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
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Fetch the exact existing record from the database to prevent tampering
            var profileToUpdate = await _context.RecurringProfiles
                .FirstOrDefaultAsync(p => p.Id == Input.Id && p.CreatedByUserId == userId);

            if (profileToUpdate == null) return NotFound();

            // 2. Only update the safe, allowed fields
            profileToUpdate.ProfileName = Input.ProfileName;
            profileToUpdate.Frequency = Input.Frequency;
            profileToUpdate.NextRunDate = Input.NextRunDate;
            profileToUpdate.AutoSubmitToMyInvois = Input.AutoSubmitToMyInvois;

            // 3. Log the activity
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                UserId = userId ?? "",
                UserName = User.Identity?.Name ?? "System",
                Action = $"Updated Recurring Profile: {profileToUpdate.ProfileName}",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            });

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Profile updated successfully.";
            return RedirectToPage("./Index");
        }
    }
}