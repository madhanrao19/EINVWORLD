using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Invoices
{
    /// <summary>
    /// DEPRECATED — superseded by <c>InvoiceDetails2</c>. Kept only as a permanent redirect so that
    /// invoice links in already-sent emails (which used /Invoices/InvoiceDetails/{uuid}) keep working.
    /// New email links point straight at InvoiceDetails2 (EmailConfiguration:EmailBaseUrls:InvoiceBaseUrl).
    /// All viewing/authorization logic lives in InvoiceDetails2 (which enforces the IDOR ownership guard).
    /// </summary>
    [Authorize]
    public class InvoiceDetailsModel : PageModel
    {
        public IActionResult OnGet(string uuid, bool fromEmail = false)
            => RedirectToPage("InvoiceDetails2", new { uuid, fromEmail });
    }
}
