using System.Threading;
using System.Threading.Tasks;

namespace EINVWORLD.Services.AI
{
    /// <summary>
    /// A swappable AI transport (Ollama today; OpenAI/Azure/Claude/Gemini in future). Implementations
    /// own only the wire protocol — they hold no business/domain logic. Selected at runtime by
    /// <see cref="AiService"/> matching <see cref="Name"/> against <see cref="AiSettings.Provider"/>.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>Provider identifier, matched case-insensitively against configuration (e.g. "Ollama").</summary>
        string Name { get; }

        /// <summary>Runs one chat completion. Returns a typed result (never throws for provider/transport errors).</summary>
        Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken ct = default);

        /// <summary>Lightweight connectivity + model-availability probe for the admin "Test connection" action.</summary>
        Task<AiProbeResult> ProbeAsync(CancellationToken ct = default);
    }
}
