using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.AI
{
    /// <summary>
    /// Provider-agnostic AI orchestrator. Resolves the configured <see cref="IAiProvider"/> from all
    /// registered providers, enforces the master enable switch, applies the default sampling settings,
    /// and guarantees a non-throwing, typed result to every caller so AI stays strictly optional.
    /// </summary>
    /// <remarks>
    /// Structured logging here is deliberately metadata-only: provider, model, outcome and duration.
    /// Prompt/response CONTENT is never logged — prompts can carry invoice and customer data, and any
    /// configured API key is never emitted.
    /// </remarks>
    public sealed class AiService : IAiService
    {
        private readonly AiSettings _settings;
        private readonly IAiProvider? _provider;
        private readonly ILogger<AiService> _log;

        public AiService(AiSettings settings, IEnumerable<IAiProvider> providers, ILogger<AiService> log)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            var all = providers?.ToList() ?? new List<IAiProvider>();
            _provider = all.FirstOrDefault(
                p => string.Equals(p.Name, _settings.Provider, StringComparison.OrdinalIgnoreCase));

            if (_settings.Enabled && _provider is null)
            {
                _log.LogWarning(
                    "AI is enabled but no provider matches '{Provider}'. Registered: [{Registered}]. AI calls will be disabled.",
                    _settings.Provider, string.Join(", ", all.Select(p => p.Name)));
            }
        }

        public bool IsEnabled => _settings.Enabled && _provider is not null;

        public string ProviderName => _settings.Provider;

        public string Model => _settings.Model;

        public async Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken ct = default)
        {
            if (request is null || request.Messages is null || request.Messages.Count == 0)
                return AiChatResult.Fail(AiErrorKind.InvalidRequest, "No prompt was supplied to the AI service.");

            if (!_settings.Enabled)
                return AiChatResult.Fail(AiErrorKind.Disabled,
                    "The AI features are disabled. Set AI:Enabled=true and configure a provider to enable them.");

            if (_provider is null)
                return AiChatResult.Fail(AiErrorKind.Disabled,
                    $"No AI provider is configured for '{_settings.Provider}'.");

            // Apply configured defaults where the caller did not override.
            var effective = new AiChatRequest
            {
                Messages = request.Messages,
                JsonMode = request.JsonMode,
                Temperature = request.Temperature ?? _settings.Temperature,
                MaxTokens = request.MaxTokens ?? _settings.MaxTokens,
            };

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _provider.ChatAsync(effective, ct).ConfigureAwait(false);
                sw.Stop();
                if (result.Ok)
                    _log.LogInformation("AI chat ok via {Provider}/{Model} in {Elapsed}ms.",
                        _provider.Name, _settings.Model, sw.ElapsedMilliseconds);
                else
                    _log.LogWarning("AI chat failed via {Provider}/{Model} ({Kind}) in {Elapsed}ms.",
                        _provider.Name, _settings.Model, result.ErrorKind, sw.ElapsedMilliseconds);
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Genuine caller cancellation — surface it.
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                // The contract is "never throw into business logic"; a provider that violates it is contained here.
                _log.LogError(ex, "AI provider {Provider} threw unexpectedly after {Elapsed}ms.",
                    _provider.Name, sw.ElapsedMilliseconds);
                return AiChatResult.Fail(AiErrorKind.Unexpected, "Unexpected error talking to the AI service.");
            }
        }

        public async Task<AiProbeResult> TestConnectionAsync(CancellationToken ct = default)
        {
            if (!_settings.Enabled)
                return AiProbeResult.Unreachable("AI is disabled (AI:Enabled=false).");

            if (_provider is null)
                return AiProbeResult.Unreachable($"No AI provider is configured for '{_settings.Provider}'.");

            try
            {
                return await _provider.ProbeAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "AI provider {Provider} probe threw unexpectedly.", _provider.Name);
                return AiProbeResult.Unreachable($"Probe failed: {ex.Message}");
            }
        }
    }
}
