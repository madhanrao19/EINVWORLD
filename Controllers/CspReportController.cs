using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public-forms")]
        public async Task<IActionResult> Report()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var buffer = new char[MaxBodyBytes];
                var read = await reader.ReadBlockAsync(buffer, 0, MaxBodyBytes);
                var body = new string(buffer, 0, read);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    foreach (var (directive, blocked, document, source) in Parse(body))
                    {
                        var key = directive + "|" + blocked + "|" + document + "|" + source;
                        if (ShouldLog(key))
                            _logger.LogInformation(
                                "CSP violation (distinct): directive={Directive} blocked={Blocked} on={Document} source={Source}",
                                directive, blocked, document, source);
                    }
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

        /// <summary>
        /// Reads violations from either report format: the classic <c>{"csp-report":{…}}</c> (report-uri) or the
        /// Reporting API's array of <c>{"type":"csp-violation","body":{…}}</c> (report-to). URLs are reduced to
        /// origin + path, so query strings (reset tokens, invoice numbers, TINs) and fragments are never logged;
        /// the script sample is never read.
        /// </summary>
        internal static List<(string Directive, string Blocked, string Document, string Source)> Parse(string body)
        {
            var found = new List<(string, string, string, string)>();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var rep))
                {
                    found.Add(From(rep, "effective-directive", "violated-directive", "blocked-uri", "document-uri", "source-file", "line-number"));
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in root.EnumerateArray())
                    {
                        if (found.Count >= 20) break; // a report batch is small; ignore anything larger
                        if (Str(r, "type") == "csp-violation" && r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.Object)
                            found.Add(From(b, "effectiveDirective", "violatedDirective", "blockedURL", "documentURL", "sourceFile", "lineNumber"));
                    }
                }
            }
            catch (JsonException) { }
            return found;
        }

        private static (string, string, string, string) From(JsonElement e, string dir, string dir2, string blocked, string doc, string src, string line)
        {
            var source = StripUrl(Str(e, src));
            if (e.TryGetProperty(line, out var l) && l.ValueKind == JsonValueKind.Number) source += ":" + l.GetRawText();
            return (Clip(Str(e, dir) ?? Str(e, dir2) ?? "?"), StripUrl(Str(e, blocked)), StripUrl(Str(e, doc)), source);
        }

        /// <summary>Keeps scheme, host and path of a URL (or a keyword such as inline / eval); drops query and fragment.</summary>
        internal static string StripUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";
            if (Uri.TryCreate(value, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
                return Clip(u.GetLeftPart(UriPartial.Path));
            var cut = value.IndexOfAny(new[] { '?', '#', ',' }); // data:/blob: payloads and anything else: just the start
            return Clip(cut >= 0 ? value[..cut] : value);
        }

        private static string Clip(string s) => s.Length <= 200 ? s : s[..200];

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
