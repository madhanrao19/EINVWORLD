using System.Collections.Generic;

namespace EINVWORLD.Services.AI
{
    /// <summary>One message in a chat exchange. <c>Role</c> is "system", "user" or "assistant".</summary>
    public sealed record AiMessage(string Role, string Content);

    /// <summary>Provider-neutral chat request. Callers build this; each provider translates it to its own wire format.</summary>
    public sealed class AiChatRequest
    {
        /// <summary>Ordered conversation, typically a system prompt followed by user/assistant turns.</summary>
        public IReadOnlyList<AiMessage> Messages { get; init; } = new List<AiMessage>();

        /// <summary>When true, ask the provider to constrain output to a single JSON object.</summary>
        public bool JsonMode { get; init; }

        /// <summary>Per-call temperature override. Null = use the configured <see cref="AiSettings.Temperature"/>.</summary>
        public double? Temperature { get; init; }

        /// <summary>Per-call max-tokens override. Null = use the configured <see cref="AiSettings.MaxTokens"/>.</summary>
        public int? MaxTokens { get; init; }
    }

    /// <summary>Categorises why an AI call did not succeed, so callers/tests can branch without string matching.</summary>
    public enum AiErrorKind
    {
        None = 0,
        /// <summary>AI is switched off, or no provider matched the configured name.</summary>
        Disabled,
        /// <summary>The provider endpoint could not be reached (daemon down, wrong URL).</summary>
        Unreachable,
        /// <summary>The request exceeded the configured timeout.</summary>
        Timeout,
        /// <summary>The provider responded but the model is missing / not pulled.</summary>
        ModelUnavailable,
        /// <summary>The provider returned a non-success status for another reason.</summary>
        ProviderError,
        /// <summary>The call succeeded transport-wise but produced no usable content.</summary>
        EmptyResponse,
        /// <summary>Bad input (e.g. empty prompt).</summary>
        InvalidRequest,
        /// <summary>Anything unexpected.</summary>
        Unexpected,
    }

    /// <summary>
    /// Outcome of an AI chat call. Always returned — never thrown — so business logic (invoice creation,
    /// document capture) can treat AI as strictly optional and continue on failure.
    /// </summary>
    public sealed class AiChatResult
    {
        public bool Ok { get; init; }
        public string Content { get; init; } = string.Empty;
        public string? Error { get; init; }
        public AiErrorKind ErrorKind { get; init; }

        public static AiChatResult Success(string content) =>
            new() { Ok = true, Content = content, ErrorKind = AiErrorKind.None };

        public static AiChatResult Fail(AiErrorKind kind, string error) =>
            new() { Ok = false, Error = error, ErrorKind = kind };
    }

    /// <summary>Result of a connectivity/health probe, used by the admin "Test connection" action.</summary>
    public sealed class AiProbeResult
    {
        /// <summary>The provider endpoint answered.</summary>
        public bool Reachable { get; init; }

        /// <summary>The configured model is present and ready (best-effort; not all providers can report this).</summary>
        public bool ModelAvailable { get; init; }

        /// <summary>Round-trip time of the probe, when measured.</summary>
        public long? LatencyMs { get; init; }

        /// <summary>Human-readable detail for the admin UI (e.g. "reachable, model 'gemma3:12b' not pulled").</summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>Models the provider reports as available, when it can enumerate them.</summary>
        public IReadOnlyList<string> AvailableModels { get; init; } = new List<string>();

        public static AiProbeResult Unreachable(string detail, long? latencyMs = null) =>
            new() { Reachable = false, ModelAvailable = false, Detail = detail, LatencyMs = latencyMs };
    }
}
