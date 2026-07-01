using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.AI.Providers
{
    /// <summary>
    /// Ollama-backed provider: talks to a local Ollama daemon over HTTP. FOSS + on-prem — invoice data
    /// never leaves the server. Translates the provider-neutral <see cref="AiChatRequest"/> to Ollama's
    /// <c>/api/chat</c> wire format and maps transport failures to typed <see cref="AiChatResult"/>s.
    /// </summary>
    public sealed class OllamaAiProvider : IAiProvider
    {
        private readonly HttpClient _http;
        private readonly AiSettings _settings;
        private readonly ILogger<OllamaAiProvider> _log;

        public OllamaAiProvider(HttpClient http, AiSettings settings, ILogger<OllamaAiProvider> log)
        {
            _http = http;
            _settings = settings;
            _log = log;
        }

        public string Name => "Ollama";

        public async Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken ct = default)
        {
            var options = new OllamaOptions
            {
                Temperature = request.Temperature ?? _settings.Temperature,
                NumPredict = request.MaxTokens is > 0 ? request.MaxTokens : null,
            };

            var payload = new OllamaChatRequest
            {
                Model = _settings.Model,
                Stream = false,
                Format = request.JsonMode ? "json" : null,
                Options = options,
                Messages = request.Messages
                    .Select(m => new OllamaMessage { Role = m.Role, Content = m.Content })
                    .ToList(),
            };

            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var resp = await _http.PostAsync("/api/chat", content, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    // Body here is Ollama's own error JSON (e.g. "model not found") — no user prompt content.
                    _log.LogWarning("Ollama /api/chat returned {Status}: {Body}", (int)resp.StatusCode, Truncate(body, 300));
                    var kind = LooksLikeMissingModel(resp.StatusCode, body) ? AiErrorKind.ModelUnavailable : AiErrorKind.ProviderError;
                    var msg = kind == AiErrorKind.ModelUnavailable
                        ? $"The model '{_settings.Model}' is not available in Ollama. Pull it first: ollama pull {_settings.Model}"
                        : $"AI service error ({(int)resp.StatusCode}).";
                    return AiChatResult.Fail(kind, msg);
                }

                var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body);
                var text = parsed?.Message?.Content?.Trim();

                return string.IsNullOrEmpty(text)
                    ? AiChatResult.Fail(AiErrorKind.EmptyResponse, "The AI returned an empty response.")
                    : AiChatResult.Success(text);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return AiChatResult.Fail(AiErrorKind.Timeout,
                    $"The AI request timed out after {_settings.TimeoutSeconds}s. The model may still be loading — try again.");
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning(ex, "Could not reach Ollama at {Url}", _settings.BaseUrl);
                return AiChatResult.Fail(AiErrorKind.Unreachable,
                    $"Could not reach the local AI service at {_settings.BaseUrl}. Is Ollama running?");
            }
        }

        public async Task<AiProbeResult> ProbeAsync(CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var resp = await _http.GetAsync("/api/tags", ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                sw.Stop();

                if (!resp.IsSuccessStatusCode)
                    return AiProbeResult.Unreachable(
                        $"Reached {_settings.BaseUrl} but /api/tags returned {(int)resp.StatusCode}.", sw.ElapsedMilliseconds);

                var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(body);
                var models = tags?.Models?.Select(m => m.Name ?? string.Empty)
                    .Where(n => n.Length > 0).ToList() ?? new List<string>();

                var modelPresent = models.Any(n => ModelMatches(n, _settings.Model));
                var detail = modelPresent
                    ? $"Reachable. Model '{_settings.Model}' is available."
                    : $"Reachable, but model '{_settings.Model}' is not pulled. Run: ollama pull {_settings.Model}";

                return new AiProbeResult
                {
                    Reachable = true,
                    ModelAvailable = modelPresent,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Detail = detail,
                    AvailableModels = models,
                };
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                return AiProbeResult.Unreachable(
                    $"Timed out reaching {_settings.BaseUrl} after {_settings.TimeoutSeconds}s.", sw.ElapsedMilliseconds);
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _log.LogWarning(ex, "Ollama probe could not reach {Url}", _settings.BaseUrl);
                return AiProbeResult.Unreachable($"Could not reach {_settings.BaseUrl}. Is Ollama running?", sw.ElapsedMilliseconds);
            }
        }

        /// <summary>Ollama tags list bare model names ("gemma3:12b"); a config of "gemma3" implies ":latest".</summary>
        private static bool ModelMatches(string available, string configured)
        {
            if (string.Equals(available, configured, StringComparison.OrdinalIgnoreCase)) return true;
            if (!configured.Contains(':') &&
                available.StartsWith(configured + ":", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool LooksLikeMissingModel(System.Net.HttpStatusCode status, string body) =>
            status == System.Net.HttpStatusCode.NotFound ||
            (body?.Contains("model", StringComparison.OrdinalIgnoreCase) == true &&
             body.Contains("not found", StringComparison.OrdinalIgnoreCase));

        private static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");

        // ---- Ollama wire DTOs ----
        private sealed class OllamaChatRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = "";
            [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; set; } = new();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("format")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Format { get; set; }
            [JsonPropertyName("options")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public OllamaOptions? Options { get; set; }
        }

        private sealed class OllamaOptions
        {
            [JsonPropertyName("temperature")] public double Temperature { get; set; }
            [JsonPropertyName("num_predict")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? NumPredict { get; set; }
        }

        private sealed class OllamaMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = "";
            [JsonPropertyName("content")] public string Content { get; set; } = "";
        }

        private sealed class OllamaChatResponse
        {
            [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        }

        private sealed class OllamaTagsResponse
        {
            [JsonPropertyName("models")] public List<OllamaTag>? Models { get; set; }
        }

        private sealed class OllamaTag
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
        }
    }
}
