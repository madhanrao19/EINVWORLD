using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Controllers.Api
{
    /// <summary>
    /// REST endpoint for an external ERP to validate invoice rows against the real LHDN reference codes
    /// before they are keyed in — the foundation for ERP integration. Validation-only for now: it never
    /// creates or submits anything. Authenticated with a static API key (header X-Api-Key) rather than
    /// the cookie, so a server-to-server caller can use it. Disabled until Api:Key is configured.
    /// </summary>
    [ApiController]
    [Route("api/import")]
    public class InvoiceImportApiController : ControllerBase
    {
        private readonly IBulkInvoiceImportService _import;
        private readonly IConfiguration _config;
        private readonly IAuditService _audit;
        private readonly ILogger<InvoiceImportApiController> _logger;

        public InvoiceImportApiController(IBulkInvoiceImportService import, IConfiguration config,
            IAuditService audit, ILogger<InvoiceImportApiController> logger)
        {
            _import = import;
            _config = config;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>POST a JSON array of row objects (column → value); returns a per-row validation report.</summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Validate([FromBody] List<Dictionary<string, JsonElement>>? rows, CancellationToken ct)
        {
            var configuredKey = _config["Api:Key"];
            if (string.IsNullOrWhiteSpace(configuredKey))
                return StatusCode(503, new { error = "Import API is disabled. Set Api:Key on the server to enable it." });

            if (!Request.Headers.TryGetValue("X-Api-Key", out var provided) || !KeysMatch(provided.ToString(), configuredKey))
                return Unauthorized(new { error = "Invalid or missing X-Api-Key." });

            if (rows is null || rows.Count == 0)
                return BadRequest(new { error = "Body must be a non-empty JSON array of row objects." });

            // Normalise every value to a string so numbers/bools from the ERP are accepted too.
            var stringRows = rows
                .Select(r => r.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ValueKind == JsonValueKind.String ? (kv.Value.GetString() ?? string.Empty) : kv.Value.ToString(),
                    System.StringComparer.OrdinalIgnoreCase))
                .ToList<Dictionary<string, string>>();

            var preview = await _import.ValidateRowsAsync(stringRows, ct);

            await _audit.WriteAsync("ApiImportValidated", new AuditEntry
            {
                NewValueJson = JsonSerializer.Serialize(new { rows = preview.TotalRows, errorRows = preview.ErrorRows }),
                UserNameOverride = "API"
            });

            return Ok(new
            {
                totalRows = preview.TotalRows,
                okRows = preview.OkRows,
                errorRows = preview.ErrorRows,
                parseError = preview.ParseError,
                rows = preview.Rows.Select(r => new
                {
                    r.RowNumber,
                    hasError = r.HasError,
                    issues = r.Issues.Select(i => new { severity = i.Severity.ToString(), i.Column, i.Message })
                })
            });
        }

        private static bool KeysMatch(string provided, string configured)
        {
            var a = Encoding.UTF8.GetBytes(provided);
            var b = Encoding.UTF8.GetBytes(configured);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
