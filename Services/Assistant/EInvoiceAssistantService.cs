using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EINVWORLD.Services.AI;

namespace EINVWORLD.Services.Assistant
{
    /// <summary>One turn of a conversation. <c>Role</c> is "user" or "assistant".</summary>
    public sealed record ChatTurn(string Role, string Content);

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
        /// Multi-turn version of <see cref="AskAsync(string,CancellationToken)"/>: continues a conversation
        /// given the prior turns plus the new question, so the assistant keeps context.
        /// </summary>
        Task<AssistantResult> AskAsync(IReadOnlyList<ChatTurn> history, string question, CancellationToken ct = default);

        /// <summary>
        /// Explains an LHDN MyInvois rejection / validation error in plain English and lists concrete
        /// steps to fix it. Advisory only — it never resubmits anything.
        /// </summary>
        Task<AssistantResult> ExplainRejectionAsync(string rejectionDetails, CancellationToken ct = default);

        /// <summary>
        /// Turns a plain-English transaction description into a structured invoice suggestion (JSON)
        /// the user can review before creating a draft. The model never submits anything itself.
        /// When <paramref name="knownBuyers"/> is supplied, the model is told to pick the buyer from that
        /// list (the user's real customers) and use its exact TIN, rather than inventing one.
        /// </summary>
        Task<AssistantResult> SuggestInvoiceAsync(
            string description, IReadOnlyList<KnownBuyer>? knownBuyers = null, CancellationToken ct = default);

        /// <summary>
        /// Validates a suggestion JSON against the real LHDN reference data + basic rules and returns a
        /// readiness checklist. Catches model hallucinations (bad codes, missing fields) before the
        /// suggestion is loaded into the form. When <paramref name="knownBuyerTins"/> is supplied, the
        /// buyer TIN is checked against the user's real customers.
        /// </summary>
        SuggestionReview ReviewSuggestion(string suggestionJson, IReadOnlyCollection<string>? knownBuyerTins = null);
    }

    /// <summary>
    /// E-invoicing domain assistant. Owns the LHDN-specific prompts, reference-data grounding and
    /// output validation, and delegates the actual model call to the provider-agnostic
    /// <see cref="IAiService"/> — so it never references a concrete backend (Ollama today) directly.
    /// Fails gracefully (returns a friendly message) when AI is disabled or the provider is unreachable.
    /// </summary>
    public sealed class EInvoiceAssistantService : IEInvoiceAssistantService
    {
        private readonly IAiService _ai;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<EInvoiceAssistantService> _log;

        // Reference data (classification + tax codes) is loaded once from the wwwroot JSON files. We keep
        // both a compact "code=description" string (injected into the prompt so the model can only choose
        // real codes) and a HashSet of the codes (used to validate the model's output server-side).
        private static volatile ReferenceData? _cachedReference;
        private static readonly object _referenceLock = new();

        private sealed class ReferenceData
        {
            public string ClassificationRef { get; init; } = string.Empty;
            public HashSet<string> ClassificationCodes { get; init; } = new();
            public string TaxRef { get; init; } = string.Empty;
            public HashSet<string> TaxCodes { get; init; } = new();
        }

        private const string QaSystemPrompt =
            "You are an assistant for a Malaysian e-invoicing middleware that integrates with LHDN MyInvois. " +
            "Answer concisely and practically. The 8 LHDN document types are: 01 Invoice, 02 Credit Note, " +
            "03 Debit Note, 04 Refund Note, 11 Self-billed Invoice, 12 Self-billed Credit Note, " +
            "13 Self-billed Debit Note, 14 Self-billed Refund Note. " +
            "Never claim to submit, cancel or change any document — you only advise. " +
            "If you are unsure about a specific LHDN rule, say so rather than guessing.";

        private const string RejectionSystemPrompt =
            "You help a user of a Malaysian e-invoicing middleware understand an LHDN MyInvois rejection or " +
            "validation error. Given the raw error details, explain in plain, non-technical English: " +
            "(1) what each error means, (2) the exact field or value that is wrong, and (3) concrete steps to " +
            "correct it and resubmit. When you recognise an LHDN error code (e.g. CF321 invalid TIN, " +
            "CF364/CF366 classification or tax issues, DS302 duplicate submission), give the friendly meaning. " +
            "If the details are unclear, say what additional information is needed. " +
            "You only advise — never claim to fix, cancel or resubmit anything yourself.";

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
            IAiService ai,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<EInvoiceAssistantService> log)
        {
            _ai = ai;
            _env = env;
            _config = config;
            _log = log;
        }

        public bool IsEnabled => _ai.IsEnabled;

        public Task<AssistantResult> AskAsync(string question, CancellationToken ct = default)
            => ChatAsync(QaSystemPrompt, question, jsonMode: false, ct);

        public Task<AssistantResult> AskAsync(IReadOnlyList<ChatTurn> history, string question, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                return Task.FromResult(AssistantResult.Fail("Please enter a question."));

            var messages = new List<AiMessage> { new("system", QaSystemPrompt) };

            // Carry a bounded slice of prior turns so the model has context without an unbounded prompt.
            if (history is { Count: > 0 })
            {
                foreach (var turn in history.TakeLast(12))
                {
                    if (string.IsNullOrWhiteSpace(turn.Content)) continue;
                    var role = turn.Role == "assistant" ? "assistant" : "user";
                    var text = turn.Content.Length > 4000 ? turn.Content[..4000] : turn.Content;
                    messages.Add(new AiMessage(role, text));
                }
            }

            messages.Add(new AiMessage("user", question));
            return ChatMessagesAsync(messages, jsonMode: false, ct);
        }

        public Task<AssistantResult> ExplainRejectionAsync(string rejectionDetails, CancellationToken ct = default)
            => ChatAsync(RejectionSystemPrompt, rejectionDetails, jsonMode: false, ct);

        public Task<AssistantResult> SuggestInvoiceAsync(
            string description, IReadOnlyList<KnownBuyer>? knownBuyers = null, CancellationToken ct = default)
            => ChatAsync(BuildSuggestSystemPrompt(knownBuyers), description, jsonMode: true, ct);

        public SuggestionReview ReviewSuggestion(string suggestionJson, IReadOnlyCollection<string>? knownBuyerTins = null)
        {
            var reference = GetReference();
            var suggestion = InvoiceSuggestionValidator.TryParse(suggestionJson);
            var buyerSet = knownBuyerTins is { Count: > 0 }
                ? new HashSet<string>(knownBuyerTins, StringComparer.OrdinalIgnoreCase)
                : null;
            return InvoiceSuggestionValidator.Review(suggestion, reference.ClassificationCodes, reference.TaxCodes, buyerSet);
        }

        private string BuildSuggestSystemPrompt(IReadOnlyList<KnownBuyer>? knownBuyers)
        {
            var reference = GetReference();
            var prompt = SuggestSystemPromptBase;

            if (!string.IsNullOrEmpty(reference.ClassificationRef))
                prompt += " The \"classificationCode\" MUST be exactly one of the codes from this LHDN classification list " +
                          "(use only the 3-digit code on the left of '='); choose the single closest match, and if nothing " +
                          "fits well use \"022\" (Others). List: " + reference.ClassificationRef;

            if (!string.IsNullOrEmpty(reference.TaxRef))
                prompt += " The \"taxType\" MUST be one of these LHDN tax codes (use only the code on the left of '='): "
                          + reference.TaxRef;

            if (knownBuyers is { Count: > 0 })
            {
                var list = string.Join("; ", knownBuyers
                    .Where(b => !string.IsNullOrWhiteSpace(b.Tin) && !string.IsNullOrWhiteSpace(b.Name))
                    .Take(100)
                    .Select(b => $"{b.Name.Trim()}={b.Tin.Trim()}"));

                if (list.Length > 0)
                    prompt += " The buyer MUST be chosen from this list of the user's known customers (format Name=TIN): " +
                              "set \"buyerName\" and \"buyerTin\" to the single best-matching entry, copying its TIN exactly. " +
                              "If none clearly match, leave \"buyerName\" and \"buyerTin\" blank and explain in \"notes\". " +
                              "Never invent a TIN. List: " + list;
            }

            return prompt;
        }

        /// <summary>Loads the LHDN classification + tax codes once and caches compact references and code sets.</summary>
        private ReferenceData GetReference()
        {
            if (_cachedReference != null) return _cachedReference;
            lock (_referenceLock)
            {
                if (_cachedReference != null) return _cachedReference;

                var (classRef, classCodes) = LoadCodes("CodeFilePaths:ClassificationCodes", "codes/ClassificationCodes.json");
                var (taxRef, taxCodes) = LoadCodes("CodeFilePaths:TaxTypes", "codes/TaxTypes.json");

                _cachedReference = new ReferenceData
                {
                    ClassificationRef = classRef,
                    ClassificationCodes = classCodes,
                    TaxRef = taxRef,
                    TaxCodes = taxCodes
                };
                return _cachedReference;
            }
        }

        /// <summary>Reads a {Code,Description} JSON code file into a compact "code=desc; ..." string and a code set.</summary>
        private (string Reference, HashSet<string> Codes) LoadCodes(string configKey, string defaultRelPath)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var rel = (_config[configKey] ?? defaultRelPath).Replace('/', Path.DirectorySeparatorChar);
                var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
                    ? Path.Combine(_env.ContentRootPath, "wwwroot")
                    : _env.WebRootPath;
                var path = Path.Combine(webRoot, rel);

                if (!File.Exists(path))
                {
                    _log.LogWarning("Assistant: code file not found at {Path}", path);
                    return (string.Empty, codes);
                }

                var list = JsonSerializer.Deserialize<List<ClassificationCodeDto>>(File.ReadAllText(path)) ?? new();
                var sb = new StringBuilder();
                foreach (var c in list)
                {
                    if (string.IsNullOrWhiteSpace(c.Code)) continue;
                    var code = c.Code.Trim();
                    codes.Add(code);
                    var desc = (c.Description ?? string.Empty).Trim();
                    if (desc.Length > 90) desc = desc.Substring(0, 90);
                    sb.Append(code).Append('=').Append(desc).Append("; ");
                }
                return (sb.ToString().TrimEnd(), codes);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Assistant: failed to load code file {Key}; that grounding/validation will be skipped.", configKey);
                return (string.Empty, codes);
            }
        }

        private Task<AssistantResult> ChatAsync(string systemPrompt, string userInput, bool jsonMode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                return Task.FromResult(AssistantResult.Fail("Please enter a question or description."));

            var messages = new List<AiMessage>
            {
                new("system", systemPrompt),
                new("user", userInput)
            };
            return ChatMessagesAsync(messages, jsonMode, ct);
        }

        /// <summary>
        /// Delegates to the provider-agnostic AI service and adapts its typed result to an
        /// <see cref="AssistantResult"/>. All transport/timeout/unreachable handling lives in the
        /// provider layer, so this method never talks HTTP and never throws for AI failures.
        /// </summary>
        private async Task<AssistantResult> ChatMessagesAsync(List<AiMessage> messages, bool jsonMode, CancellationToken ct)
        {
            var result = await _ai.ChatAsync(new AiChatRequest { Messages = messages, JsonMode = jsonMode }, ct);
            return result.Ok
                ? AssistantResult.Success(result.Content)
                : AssistantResult.Fail(result.Error ?? "Unexpected error talking to the AI service.");
        }

        private sealed class ClassificationCodeDto
        {
            [JsonPropertyName("Code")] public string? Code { get; set; }
            [JsonPropertyName("Description")] public string? Description { get; set; }
        }
    }
}
