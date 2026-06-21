using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Controllers
{
    /// <summary>
    /// Receives Content-Security-Policy violation reports (the CSP ships in Report-Only mode and points
    /// its report-uri here). Logging real violations is the prerequisite for tightening the policy and
    /// eventually promoting it from Report-Only to enforcing — without it, "collect violations" is a
    /// manual chore. Anonymous + tiny: browsers post these without credentials.
    /// </summary>
    [ApiController]
    public class CspReportController : ControllerBase
    {
        private const int MaxBodyBytes = 16 * 1024; // ignore anything implausibly large

        private readonly ILogger<CspReportController> _logger;

        public CspReportController(ILogger<CspReportController> logger) => _logger = logger;

        [HttpPost("/csp-report")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken] // cross-origin browser report, no antiforgery token
        public async Task<IActionResult> Report()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var buffer = new char[MaxBodyBytes];
                var read = await reader.ReadBlockAsync(buffer, 0, MaxBodyBytes);
                var body = new string(buffer, 0, read);

                if (!string.IsNullOrWhiteSpace(body))
                    _logger.LogWarning("CSP violation report: {Report}", body);
            }
            catch
            {
                // A malformed report must never error the browser — just swallow it.
            }

            return NoContent();
        }
    }
}
