using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.AI;
using EINVWORLD.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Admin
{
    /// <summary>
    /// Admin view of the current AI configuration plus a "Test connection" action. Read-only: it never
    /// edits config (settings live in appsettings / env vars) and never displays or logs the API key.
    /// The test button probes the configured provider so an admin can confirm the model is reachable and
    /// pulled before enabling AI features.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AiSettingsModel : PageModel
    {
        private readonly IAiService _ai;
        private readonly AiSettings _settings;
        private readonly IAuditService _audit;

        public AiSettingsModel(IAiService ai, AiSettings settings, IAuditService audit)
        {
            _ai = ai;
            _settings = settings;
            _audit = audit;
        }

        // ── Display-only configuration (never the API key) ─────────────────────────────────────
        public bool Enabled => _settings.Enabled;
        public string Provider => _settings.Provider;
        public string BaseUrl => _settings.BaseUrl;
        public string Model => _settings.Model;
        public int TimeoutSeconds => _settings.TimeoutSeconds;
        public double Temperature => _settings.Temperature;
        public int MaxTokens => _settings.MaxTokens;

        /// <summary>True when AI is enabled AND a provider matching the configured name is registered.</summary>
        public bool ProviderReady => _ai.IsEnabled;

        /// <summary>Populated after a "Test connection" post.</summary>
        public AiProbeResult? ProbeResult { get; private set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostTestConnectionAsync(CancellationToken ct)
        {
            ProbeResult = await _ai.TestConnectionAsync(ct);

            // Audit the outcome only — no prompts, no secrets. Serialized (not string-built) so an unusual
            // provider/model name can never produce malformed JSON.
            var outcome = JsonSerializer.Serialize(new
            {
                provider = Provider,
                model = Model,
                reachable = ProbeResult.Reachable,
                modelAvailable = ProbeResult.ModelAvailable,
                latencyMs = ProbeResult.LatencyMs,
            });
            await _audit.WriteAsync("AiConnectionTested", new AuditEntry { NewValueJson = outcome }, ct);

            return Page();
        }
    }
}
