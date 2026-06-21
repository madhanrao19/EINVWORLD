using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models.Background;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Executes one kind of durable background job (keyed by <see cref="JobType"/>). The durable
    /// worker reconstructs the work purely from the persisted <see cref="SyncJob"/> row, so the
    /// handler must derive everything it needs from the row (Tin, TriggeredBy, PayloadJson) — never
    /// from a captured closure. Throw to signal failure (the worker handles retry/backoff).
    /// </summary>
    public interface ISyncJobHandler
    {
        /// <summary>The <see cref="SyncJobType"/> value this handler processes.</summary>
        string JobType { get; }

        /// <summary>Runs the job; returns a human-readable result message stored on completion.</summary>
        Task<string> ExecuteAsync(SyncJob job, CancellationToken ct);
    }

    /// <summary>Serializable parameters carried on a <see cref="SyncJob"/> (kept tiny and durable).</summary>
    public sealed class SyncJobPayload
    {
        public int? LookbackDays { get; set; }

        private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

        public static string Create(int lookbackDays) =>
            JsonSerializer.Serialize(new SyncJobPayload { LookbackDays = lookbackDays });

        /// <summary>Reads LookbackDays from the job payload, falling back to <paramref name="fallback"/>.</summary>
        public static int LookbackOrDefault(string? payloadJson, int fallback)
        {
            if (string.IsNullOrWhiteSpace(payloadJson)) return fallback;
            try
            {
                var p = JsonSerializer.Deserialize<SyncJobPayload>(payloadJson, Opts);
                return p?.LookbackDays is int d && d > 0 ? d : fallback;
            }
            catch (JsonException)
            {
                return fallback;
            }
        }
    }
}
