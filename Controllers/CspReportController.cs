using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Controllers
{
    /// <summary>
    /// Receives Content-Security-Policy violation reports (the CSP ships in Report-Only mode and points
    /// its report-uri here). The point is to collect the DISTINCT violations needed to tighten the policy
    /// before promoting it to enforcing — not to flood the log. So each unique violation
    /// (directive + blocked resource) is logged at most once per window, at Information level.
    /// Anonymous: browsers post these without credentials.
    /// </summary>
    [ApiController]
    public class CspReportController : ControllerBase
    {
        private const int MaxBodyBytes = 16 * 1024; // ignore anything implausibly large
        private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(10);
        private const int MaxTrackedKeys = 2000; // bound memory; reset if exceeded

        // Last time each distinct "directive|blocked-uri" violation was logged. Static = process-wide.
        private static readonly ConcurrentDictionary<string, DateTime> LastLogged = new();

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

                if (!string.IsNullOrWhiteSpace(body) && TryParse(body, out var directive, out var blocked, out var documentUri))
                {
                    var key = directive + "|" + blocked;
                    if (ShouldLog(key))
                        _logger.LogInformation(
                            "CSP violation (distinct): directive={Directive} blocked={Blocked} on={Document}",
                            directive, blocked, documentUri);
                }
            }
            catch
            {
                // A malformed report must never error the browser — just swallow it.
            }

            return NoContent();
        }

        /// <summary>True if this violation key hasn't been logged within the dedup window.</summary>
        private static bool ShouldLog(string key)
        {
            var now = DateTime.UtcNow;
            if (LastLogged.Count > MaxTrackedKeys) LastLogged.Clear(); // crude bound; fine for a report sink

            var fresh = false;
            LastLogged.AddOrUpdate(key,
                _ => { fresh = true; return now; },
                (_, last) =>
                {
                    if (now - last >= DedupWindow) { fresh = true; return now; }
                    return last;
                });
            return fresh;
        }

        /// <summary>Extracts (effective/violated) directive, blocked-uri and document-uri from a CSP report.</summary>
        private static bool TryParse(string body, out string directive, out string blocked, out string documentUri)
        {
            directive = blocked = documentUri = "?";
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("csp-report", out var rep))
                {
                    directive = Str(rep, "effective-directive") ?? Str(rep, "violated-directive") ?? "?";
                    blocked = Str(rep, "blocked-uri") ?? "?";
                    documentUri = Str(rep, "document-uri") ?? "?";
                    return true;
                }
            }
            catch (JsonException) { }
            return false;
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
