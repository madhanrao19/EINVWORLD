using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages
{
    /// <summary>
    /// The error page for unhandled exceptions (/Error) and for empty error responses on page visits
    /// (/Error/{code}, via UseStatusCodePagesWithReExecute). Shows the reference (correlation id) users quote to support.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        // Codes the page has wording for; any other 4xx/5xx shows the generic 400 / 500 page.
        private static readonly HashSet<int> Known = new() { 400, 401, 403, 404, 413, 429, 500, 502, 503, 504 };

        public string RequestId { get; private set; } = "";
        public int Code { get; private set; }

        public void OnGet(int? code) => Show(code);
        // The exception handler and status-code re-execute keep the original method, so a failed POST lands here.
        public void OnPost(int? code) => Show(code);

        private void Show(int? code)
        {
            // CorrelationIdMiddleware keeps TraceIdentifier equal to the request's correlation id.
            RequestId = HttpContext.TraceIdentifier;
            var c = code ?? 500;
            Code = Known.Contains(c) ? c : c >= 500 ? 500 : 400;
            Response.StatusCode = c is >= 400 and <= 599 ? c : 500;
        }
    }
}
