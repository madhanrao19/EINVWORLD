using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EINVWORLD.Services.Assistant
{
    /// <summary>Bound from the "AIAssistant" config section.</summary>
    public sealed class AIAssistantOptions
    {
        public const string SectionName = "AIAssistant";

        /// <summary>Master switch. OFF by default — the assistant only works once a local LLM is running.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Currently only "Ollama" is implemented (local, FOSS, on-prem — no invoice data leaves the server).</summary>
        public string Provider { get; set; } = "Ollama";

        /// <summary>Ollama endpoint. Default is the standard local Ollama port.</summary>
        public string BaseUrl { get; set; } = "http://localhost:11434";

        /// <summary>Open-weight model name pulled into Ollama, e.g. "llama3.1", "qwen2.5", "mistral".</summary>
        public string Model { get; set; } = "llama3.1";

        public int TimeoutSeconds { get; set; } = 120;
    }

    public sealed class AssistantResult
    {
        public bool Ok { get; init; }
        public string Content { get; init; } = string.Empty;
        public string? Error { get; init; }

        public static AssistantResult Success(string content) => new() { Ok = true, Content = content };
        public static AssistantResult Fail(string error) => new() { Ok = false, Error = error };
    }

    public interface IEInvoiceAssistantService
    {
        bool IsEnabled { get; }

        /// <summary>Answers a free-text question about Malaysian e-invoicing / LHDN MyInvois.</summary>
        Task<AssistantResult> AskAsync(string question, CancellationToken ct = default);

        /// <summary>
        /// Turns a plain-English transaction description into a structured invoice suggestion (JSON)
        /// the user can review before creating a draft. The model never submits anything itself.
        /// </summary>
        Task<AssistantResult> SuggestInvoiceAsync(string description, CancellationToken ct = default);
    }

    /// <summary>
    /// Local-LLM (Ollama) backed assistant. Honours the FOSS-only + on-prem constraints: the model
    /// runs on the same server and no invoice data is sent to any external service. Fails gracefully
    /// (returns a friendly message) when disabled or when Ollama is unreachable.
    /// </summary>
    public sealed class EInvoiceAssistantService : IEInvoiceAssistantService
    {
        private readonly HttpClient _http;
        private readonly AIAssistantOptions _options;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<EInvoiceAssistantService> _log;

        // Classification-code reference (e.g. "001=Breastfeeding equipment; ...") is loaded once and
        // injected into the suggestion prompt so the model can only choose a real LHDN code.
        private static volatile string? _cachedClassificationRef;
        private static readonly object _classificationLock = new();

        private const string QaSystemPrompt =
            "You are an assistant for a Malaysian e-invoicing middleware that integrates with LHDN MyInvois. " +
            "Answer concisely and practically. The 8 LHDN document types are: 01 Invoice, 02 Credit Note, " +
            "03 Debit Note, 04 Refund Note, 11 Self-billed Invoice, 12 Self-billed Credit Note, " +
            "13 Self-billed Debit Note, 14 Self-billed Refund Note. " +
            "Never claim to submit, cancel or change any document — you only advise. " +
            "If you are unsure about a specific LHDN rule, say so rather than guessing.";

        private const string SuggestSystemPromptBase =
            "You are an assistant that converts a plain-English sales/purchase description into a suggested " +
            "Malaysian LHDN e-invoice. Respond with ONLY a JSON object, no prose, using this shape: " +
            "{\"documentType\":\"01\",\"documentTypeName\":\"Invoice\",\"currency\":\"MYR\"," +
            "\"buyerName\":\"\",\"buyerTin\":\"\"," +
            "\"lineItems\":[{\"description\":\"\",\"quantity\":1,\"unitPrice\":0,\"classificationCode\":\"\"}]," +
            "\"taxType\":\"\",\"taxRatePercent\":0,\"notes\":\"\"}. " +
            "Pick the most appropriate documentType from: 01 Invoice, 02 Credit Note, 03 Debit Note, 04 Refund Note, " +
            "11 Self-billed Invoice, 12 Self-billed Credit Note, 13 Self-billed Debit Note, 14 Self-billed Refund Note. " +
            "Leave a field blank if the description does not specify it. Put any assumptions or missing-info warnings in \"notes\".";

        public EInvoiceAssistantService(
            HttpClient http,
            AIAssistantOptions options,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<EInvoiceAssistantService> log)
        {
            _http = http;
            _options = options;
            _env = env;
            _config = config;
            _log = log;
        }

        public bool IsEnabled => _options.Enabled;

        public Task<AssistantResult> AskAsync(string question, CancellationToken ct = default)
            => ChatAsync(QaSystemPrompt, question, jsonMode: false, ct);

        public Task<AssistantResult> SuggestInvoiceAsync(string description, CancellationToken ct = default)
            => ChatAsync(BuildSuggestSystemPrompt(), description, jsonMode: true, ct);

        private string BuildSuggestSystemPrompt()
        {
            var codes = GetClassificationReference();
            if (string.IsNullOrEmpty(codes)) return SuggestSystemPromptBase;

            return SuggestSystemPromptBase +
                " The \"classificationCode\" MUST be exactly one of the codes from this LHDN classification list " +
                "(use only the 3-digit code on the left of '='); choose the single closest match, and if nothing " +
                "fits well use \"022\" (Others). List: " + codes;
        }

        /// <summary>Loads the LHDN classification codes once and caches a compact "code=description" reference.</summary>
        private string GetClassificationReference()
        {
            if (_cachedClassificationRef != null) return _cachedClassificationRef;
            lock (_classificationLock)
            {
                if (_cachedClassificationRef != null) return _cachedClassificationRef;
                try
                {
                    var rel = _config["CodeFilePaths:ClassificationCodes"] ?? "codes/ClassificationCodes.json";
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
                        ? Path.Combine(_env.ContentRootPath, "wwwroot")
                        : _env.WebRootPath;
                    var path = Path.Combine(webRoot, rel);

                    if (!File.Exists(path))
                    {
                        _log.LogWarning("Assistant: classification codes file not found at {Path}", path);
                        _cachedClassificationRef = string.Empty;
                        return _cachedClassificationRef;
                    }

                    var json = File.ReadAllText(path);
                    var codes = JsonSerializer.Deserialize<List<ClassificationCodeDto>>(json) ?? new();
                    var sb = new StringBuilder();
                    foreach (var c in codes)
                    {
                        if (string.IsNullOrWhiteSpace(c.Code)) continue;
                        var desc = (c.Description ?? string.Empty).Trim();
                        if (desc.Length > 90) desc = desc.Substring(0, 90);
                        sb.Append(c.Code.Trim()).Append('=').Append(desc).Append("; ");
                    }
                    _cachedClassificationRef = sb.ToString().TrimEnd();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Assistant: failed to load classification codes; suggestions will omit the code list.");
                    _cachedClassificationRef = string.Empty;
                }
                return _cachedClassificationRef;
            }
        }

        private async Task<AssistantResult> ChatAsync(string systemPrompt, string userInput, bool jsonMode, CancellationToken ct)
        {
            if (!_options.Enabled)
                return AssistantResult.Fail("The AI assistant is disabled. Set AIAssistant:Enabled=true and run a local Ollama model to enable it.");

            if (string.IsNullOrWhiteSpace(userInput))
                return AssistantResult.Fail("Please enter a question or description.");

            var payload = new OllamaChatRequest
            {
                Model = _options.Model,
                Stream = false,
                Format = jsonMode ? "json" : null,
                Messages = new()
                {
                    new OllamaMessage { Role = "system", Content = systemPrompt },
                    new OllamaMessage { Role = "user", Content = userInput }
                }
            };

            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var resp = await _http.PostAsync("/api/chat", content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("Ollama returned {Status}: {Body}", (int)resp.StatusCode, body);
                    return AssistantResult.Fail($"AI service error ({(int)resp.StatusCode}). Is the model '{_options.Model}' pulled in Ollama?");
                }

                var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body);
                var text = parsed?.Message?.Content?.Trim();

                return string.IsNullOrEmpty(text)
                    ? AssistantResult.Fail("The AI returned an empty response.")
                    : AssistantResult.Success(text);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return AssistantResult.Fail($"The AI request timed out after {_options.TimeoutSeconds}s. The model may be loading — try again.");
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning(ex, "Could not reach Ollama at {Url}", _options.BaseUrl);
                return AssistantResult.Fail($"Could not reach the local AI service at {_options.BaseUrl}. Is Ollama running?");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "AI assistant call failed");
                return AssistantResult.Fail("Unexpected error talking to the AI service.");
            }
        }

        // ---- Ollama DTOs ----
        private sealed class OllamaChatRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = "";
            [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; set; } = new();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("format")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Format { get; set; }
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

        private sealed class ClassificationCodeDto
        {
            [JsonPropertyName("Code")] public string? Code { get; set; }
            [JsonPropertyName("Description")] public string? Description { get; set; }
        }
    }
}
