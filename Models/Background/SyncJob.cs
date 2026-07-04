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

        // ── Durable-queue fields ──────────────────────────────────────────────────────────
        // The row IS the work item: a durable worker polls Queued rows, claims one (LockedBy/
        // LockedUntilUtc), runs it, and retries with backoff (AttemptCount/MaxAttempts/NextRunAtUtc)
        // so a job survives an IIS app-pool recycle / server reboot instead of vanishing.

        /// <summary>Number of times execution has been attempted (incremented on each claim).</summary>
        public int AttemptCount { get; set; }

        /// <summary>Give up and mark Failed after this many attempts.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Earliest time the job may run. NULL = run as soon as possible (used for retry backoff).</summary>
        public DateTime? NextRunAtUtc { get; set; }

        /// <summary>Worker instance that currently holds the job (machine:pid). NULL when not running.</summary>
        [MaxLength(100)]
        public string? LockedBy { get; set; }

        /// <summary>Lock expiry; a Running job past this (or any Running job after a restart) is recovered.</summary>
        public DateTime? LockedUntilUtc { get; set; }

        /// <summary>Optional JSON parameters the handler needs (e.g. {"lookbackDays":60}).</summary>
        public string? PayloadJson { get; set; }
    }

    public static class SyncJobStatus
    {
        public const string Queued = "Queued";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }

    public static class SyncJobType
    {
        public const string StatusSync = "StatusSync";
        public const string FullImport = "FullImport";
        public const string SupplierRefresh = "SupplierRefresh";

        /// <summary>Background retry of an interactive LHDN submission that threw (network blip, LHDN
        /// outage, transient error). Queued alongside the existing inline error shown to the user, so a
        /// failure is never silent: it retries automatically, and lands in Admin -&gt; Sync Jobs (Failed)
        /// for visibility/manual replay if every attempt fails.</summary>
        public const string SubmitDocument = "SubmitDocument";

        /// <summary>Durable delivery of an outbound webhook to a customer ERP when an invoice reaches a
        /// terminal LHDN status. Reuses the queue's retry/backoff/dead-letter and the Admin -&gt; Sync Jobs
        /// UI, so a receiver being down is retried automatically and surfaces as Failed if all attempts are
        /// exhausted.</summary>
        public const string WebhookDelivery = "WebhookDelivery";
    }
}
