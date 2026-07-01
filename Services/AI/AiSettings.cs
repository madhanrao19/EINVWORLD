namespace EINVWORLD.Services.AI
{
    /// <summary>
    /// Provider-agnostic AI configuration, bound from the "AI" config section (falling back to the
    /// legacy "AIAssistant" section for one release — see <c>Program.cs</c>). OFF by default: the
    /// platform never depends on AI for normal invoice operations, so nothing runs until an admin
    /// opts in and a local model is available.
    /// </summary>
    public sealed class AiSettings
    {
        /// <summary>Canonical config section name.</summary>
        public const string SectionName = "AI";

        /// <summary>Legacy section kept as a fallback for existing deployments.</summary>
        public const string LegacySectionName = "AIAssistant";

        /// <summary>Master switch. When false, every AI call returns a graceful "disabled" result.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Which provider backs the AI calls. Today only "Ollama" (local, FOSS, on-prem) is implemented;
        /// the abstraction leaves room for "OpenAI", "AzureOpenAI", "Claude", "Gemini" without touching
        /// any business logic. Matched case-insensitively against <see cref="IAiProvider.Name"/>.
        /// </summary>
        public string Provider { get; set; } = "Ollama";

        /// <summary>Provider endpoint. For Ollama this is the local daemon (default port 11434).</summary>
        public string BaseUrl { get; set; } = "http://localhost:11434";

        /// <summary>Model name. For Ollama, an open-weight model pulled locally, e.g. "gemma3:12b".</summary>
        public string Model { get; set; } = "gemma3:12b";

        /// <summary>Per-request timeout. A cold model load can take a while on first use.</summary>
        public int TimeoutSeconds { get; set; } = 120;

        /// <summary>Sampling temperature (0 = deterministic). Low by default for structured, repeatable output.</summary>
        public double Temperature { get; set; } = 0.2;

        /// <summary>Upper bound on generated tokens. 0 or negative = let the provider decide.</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// API key / bearer token for cloud providers (OpenAI/Azure/Claude/Gemini). Unused by Ollama.
        /// MUST come from env vars / user-secrets, never appsettings, and is NEVER logged or shown in the UI.
        /// </summary>
        public string? ApiKey { get; set; }
    }
}
