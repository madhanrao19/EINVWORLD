using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Background
{
    /// <summary>
    /// A record of a background LHDN sync/import job, so users can see whether a "Run Sync",
    /// "Import All" or supplier "Refresh from API" actually ran, is running, or failed —
    /// instead of the work disappearing silently into the queue.
    /// </summary>
    public class SyncJob
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The company TIN this job targets ("admin-sync" for the global status sync).</summary>
        [MaxLength(50)]
        public string Tin { get; set; } = string.Empty;

        /// <summary>StatusSync | FullImport | SupplierRefresh</summary>
        [MaxLength(40)]
        public string JobType { get; set; } = string.Empty;

        /// <summary>Queued | Running | Completed | Failed</summary>
        [MaxLength(20)]
        public string Status { get; set; } = SyncJobStatus.Queued;

        public DateTime QueuedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }

        public int ImportedCount { get; set; }
        public int ErrorCount { get; set; }

        /// <summary>Human-readable result or error summary.</summary>
        [MaxLength(2000)]
        public string? Message { get; set; }

        [MaxLength(256)]
        public string? TriggeredBy { get; set; }
    }

    public static class SyncJobStatus
    {
        public const string Queued = "Queued";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }

    public static class SyncJobType
    {
        public const string StatusSync = "StatusSync";
        public const string FullImport = "FullImport";
        public const string SupplierRefresh = "SupplierRefresh";
    }
}
