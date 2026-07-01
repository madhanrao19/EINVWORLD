using System.Threading;
using System.Threading.Tasks;

namespace EINVWORLD.Services.AI
{
    /// <summary>
    /// The single orchestration seam every AI-using feature depends on. It owns the master enable/disable
    /// switch and provider selection, so no domain code (assistant, document capture) ever references a
    /// concrete provider such as Ollama. Adding a new provider is a DI registration — callers don't change.
    /// </summary>
    public interface IAiService
    {
        /// <summary>True only when AI is enabled AND a provider matching the configured name is registered.</summary>
        bool IsEnabled { get; }

        /// <summary>The configured provider name (for display/diagnostics).</summary>
        string ProviderName { get; }

        /// <summary>The configured model name (for display/diagnostics).</summary>
        string Model { get; }

        /// <summary>
        /// Runs a chat completion through the active provider. Returns a graceful failure result when AI is
        /// disabled, misconfigured, or the provider errors — it never throws, so callers can treat AI as optional.
        /// </summary>
        Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken ct = default);

        /// <summary>Probes the active provider for the admin "Test connection" action.</summary>
        Task<AiProbeResult> TestConnectionAsync(CancellationToken ct = default);
    }
}
