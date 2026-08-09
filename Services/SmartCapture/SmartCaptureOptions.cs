using System;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>Bound from the "SmartCapture" config section. Governs the async, persisted Smart Capture
    /// pipeline (Stage 1) built on top of the existing DocumentCapture extraction services. OFF by default —
    /// like DocumentCapture, this also requires AI:Enabled since it drives the local LLM suggestion step.</summary>
    public sealed class SmartCaptureOptions
    {
        public const string SectionName = "SmartCapture";

        /// <summary>Master switch for the persisted/async Smart Capture flow.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Accepted upload extensions (lower-case, no dot). Every appsettings*.json in this repo sets this
        /// explicitly. Left empty here (not a literal default array) deliberately — the .NET configuration
        /// binder appends config values onto an already-populated array property (unlike List&lt;T&gt;,
        /// which it replaces), so a non-empty default here would silently duplicate every entry once bound.
        /// </summary>
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>Reject uploads larger than this.</summary>
        public int MaxFileSizeMb { get; set; } = 10;

        /// <summary>Only process this many pages of a document (bounds OCR/LLM work).</summary>
        public int MaxPages { get; set; } = 15;

        /// <summary>Stage 3: maximum number of files accepted in a single bulk-upload postback. Bulk
        /// upload reuses the same per-file validation/quota path as a single upload (SmartCaptureDocumentService
        /// .UploadAsync) unchanged — this only bounds how many files one HTTP request can queue at once.</summary>
        public int MaxFilesPerBulkUpload { get; set; } = 20;

        // ── Retention (tiered — see SmartCaptureRetentionJobHandler for the rules these back) ──────

        /// <summary>Documents that never got past extraction (Failed / ValidationFailed with no draft).</summary>
        public int RetentionDaysFailed { get; set; } = 14;

        /// <summary>Documents extracted and awaiting review that the user never acted on
        /// (ReviewRequired with no draft, past this many days).</summary>
        public int RetentionDaysAbandonedReview { get; set; } = 30;

        /// <summary>Documents that produced a Draft invoice still in Draft status.</summary>
        public int RetentionDaysDraftLinked { get; set; } = 180;

        /// <summary>Documents whose linked invoice has reached a submitted/terminal LHDN status — retained
        /// longer to support compliance/audit lookback.</summary>
        public int RetentionDaysSubmittedLinked { get; set; } = 2555; // ~7 years

        // ── Quota (measured in successfully processed pages per company per calendar month — retries of
        //    the same document do not multiply the charge; see SmartCaptureDocumentService.CheckQuotaAsync) ──
        public int MonthlyProcessedPageQuota { get; set; } = 500;
    }
}
