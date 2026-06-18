using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using eInvWorld.Data; // Make sure namespace matches where InvoiceSyncHelper is

namespace eInvWorld.Pages.Admin
{
    [Authorize(Roles = "Admin")] // 🔐 Protect this page
    public class InvoiceSyncModel : PageModel
    {
        private readonly InvoiceSyncHelper _syncHelper;
        private readonly ApplicationDbContext _dbContext;

        public InvoiceSyncModel(InvoiceSyncHelper syncHelper, ApplicationDbContext dbContext)
        {
            _syncHelper = syncHelper;
            _dbContext = dbContext;
        }


        public async Task<IActionResult> OnPostRunAsync()
        {
            string userName = User?.Identity?.Name ?? "System";

            var syncSummary = await _syncHelper.RunInvoiceUpdateAsync(userName);
            var finalizeSummary = await _syncHelper.RunFinalizerAsync(userName);

            TempData["Message"] = "✅ Full Invoice sync and finalization completed successfully.";
            TempData["ResultSummary"] = syncSummary + "<br/>" + finalizeSummary;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostFullImportAllAsync()
        {
            // Get all TINs linked to the current user (could be system or intermediary)
            var allUserTins = User.GetUserCompanies(_dbContext).Select(x => x.TIN).ToList();
            
            // Filter out General TINs as they cannot be used for LHDN token requests
            var userTins = allUserTins.Where(tin => !GeneralTINHelper.IsGeneralTIN(tin)).ToList();
            
            if (!userTins.Any())
            {
                var generalTinCount = allUserTins.Count - userTins.Count;
                var message = generalTinCount > 0 
                    ? $"❌ No valid companies for LHDN import. Found {generalTinCount} General TIN(s) which cannot be used for import."
                    : "❌ No companies linked to your account.";
                TempData["Message"] = message;
                return RedirectToPage();
            }

            var results = new List<string>();
            var skippedGeneralTins = allUserTins.Where(tin => GeneralTINHelper.IsGeneralTIN(tin)).ToList();
            
            foreach (var tin in userTins)
            {
                var result = await _syncHelper.RunFullImportFromLhdnAsync(tin, User?.Identity?.Name ?? "System");
                results.Add($"TIN {tin}: {result}");
            }
            
            // Add information about skipped General TINs
            if (skippedGeneralTins.Any())
            {
                results.Add($"⚠️ Skipped {skippedGeneralTins.Count} General TIN(s): {string.Join(", ", skippedGeneralTins)}");
            }

            TempData["Message"] = "✅ LHDN Full Import (All Companies) finished.";
            TempData["ResultSummary"] = string.Join("<br/>", results);
            return RedirectToPage();
        }

    }

}
