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
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public RecurringProfile Input { get; set; } = new RecurringProfile();

        // Properties used strictly for displaying the locked data on the UI
        public string BaseTemplateName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int templateId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Fetch the template and ensure the user actually owns it
            var template = await _context.InvoiceTemplates
                .Include(t => t.Supplier)
                .Include(t => t.Customer)
                .Include(t => t.PublicCustomer)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.CreatedByUserId == userId);

            if (template == null)
            {
                TempData["StatusMessage"] = "Error: Invalid or unauthorized template selected.";
                return RedirectToPage("/Templates/TemplateLists");
            }

            // 2. Populate the display fields
            BaseTemplateName = template.TemplateName ?? "Unnamed Template";
            SupplierName = template.Supplier?.CompanyName ?? "Unknown Company";
            BuyerName = template.Customer?.CompanyName ?? template.PublicCustomer?.CompanyName ?? "All Buyers";

            // 3. Pre-fill the hidden input bindings so they submit with the form
            Input.InvoiceTemplateId = template.Id;
            Input.SupplierId = template.SupplierId ?? 0;
            Input.CustomerId = template.CustomerId;
            Input.PublicCustomerId = template.PublicCustomerId;

            // 4. Set sensible defaults
            Input.NextRunDate = DateTime.Today.AddDays(1); // Default to tomorrow
            Input.Frequency = "Monthly";
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

            // Apply system-controlled fields
            Input.CreatedByUserId = userId ?? "";
            Input.CreatedAt = DateTime.UtcNow;
            Input.Status = "Active";

            _context.RecurringProfiles.Add(Input);

            // Add an audit log entry for user activity
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                UserId = userId ?? "",
                UserName = User.Identity?.Name ?? "System",
                Action = $"Created Recurring Profile: {Input.ProfileName}",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            });

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Recurring Profile created successfully.";
            return RedirectToPage("./Index");
        }
    }
}