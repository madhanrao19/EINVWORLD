using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Models.SmartCapture
{
    public static class SmartCaptureDocumentStatus
    {
        public const string Uploaded = "Uploaded";
        public const string Queued = "Queued";
        public const string Processing = "Processing";
        public const string ReviewRequired = "ReviewRequired";
        public const string ValidationFailed = "ValidationFailed";
        public const string DraftCreated = "DraftCreated";
        public const string Failed = "Failed";
    }

    /// <summary>
    /// A supplier invoice document uploaded via Smart Capture (Stage 1 of the AI Document Capture feature).
    /// Tracks the persisted original file, the normalized OCR/LLM extraction result, and the eventual Draft
    /// invoice it produced. Nothing here bypasses the normal invoice pipeline: only the confirm postback
    /// (via InvoiceDraftService.SaveDraft, which needs an interactive session) ever creates the linked
    /// InvoiceHeader row, and this table never touches InvoiceMapper.cs or the LHDN submission path.
    /// </summary>
    public class SmartCaptureDocument
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The company (PartyInfo) this document was uploaded for. Every query against this table
        /// must filter by CompanyPartyInfoId via the caller's UserCompanies membership — see
        /// SmartCaptureDocumentService, which is the single place that scoping is applied.</summary>
        [Required]
        public int CompanyPartyInfoId { get; set; }

        [ForeignKey(nameof(CompanyPartyInfoId))]
        public PartyInfo? CompanyPartyInfo { get; set; }

        [Required]
        [MaxLength(450)]
        public string UploadedByUserId { get; set; } = null!;

        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = null!;

        /// <summary>SafePath-relative storage reference under FilePathConfig.SmartCaptureFolder — never a raw
        /// absolute path, never exposed to the client directly (downloads go through a tenant-scoped
        /// endpoint that resolves this server-side).</summary>
        [Required]
        [MaxLength(300)]
        public string InternalStorageReference { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = null!;

        public long FileSize { get; set; }

        /// <summary>SHA-256 of the uploaded file, hex-encoded. Not used for duplicate detection in Stage 1
        /// (deferred to Stage 2) but cheap to capture during upload for later use.</summary>
        [MaxLength(64)]
        public string? FileHash { get; set; }

        public int? PageCount { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = SmartCaptureDocumentStatus.Uploaded;

        /// <summary>Serialized extraction result (InvoiceSuggestion + SuggestionReview) — PII-encrypted at
        /// rest via ApplicationDbContext's Encrypt&lt;T&gt; helper (may contain supplier bank details, per
        /// the fields InvoiceSuggestion can carry).</summary>
        public string? NormalizedExtractionJson { get; set; }

        public decimal? OverallConfidence { get; set; }

        public bool UsedOcr { get; set; }

        /// <summary>Set only once the user has explicitly confirmed the LHDN document type on the review
        /// screen — never auto-populated from the OCR suggestion alone. Draft creation is blocked until set.</summary>
        [MaxLength(10)]
        public string? ConfirmedDocTypeCode { get; set; }

        /// <summary>FK to InvoiceHeader.InvoiceNo — InvoiceHeader's actual primary key (there is no separate
        /// surrogate id on InvoiceHeader). Null until InvoiceDraftService.SaveDraft has created the draft.</summary>
        [MaxLength(50)]
        public string? RelatedInvoiceHeaderInvoiceNo { get; set; }

        /// <summary>Stage 4: set to the id of a delayed SyncJobType.SubmitDocument job when
        /// SmartCaptureAutoSubmitEligibilityService schedules an automatic submission for this document's
        /// draft. Null unless the company has explicitly opted in AND every gating condition passed. The
        /// user can cancel the pending job (Smart Capture list page) any time before its NextRunAtUtc — the
        /// job row itself is the single source of truth for whether it will actually run; this is only a
        /// display/lookup convenience, never re-checked as an authorization gate.</summary>
        public int? PendingAutoSubmitJobId { get; set; }

        /// <summary>Machine-stable failure category (e.g. "OcrFailed", "LlmUnavailable", "MalwareDetected").
        /// Never a raw exception message.</summary>
        [MaxLength(60)]
        public string? FailureCode { get; set; }

        /// <summary>Sanitized, user-safe failure summary shown on the review screen. Must never contain file
        /// paths, OCR text, LLM prompts, stack traces, or infrastructure details — those belong only in
        /// Serilog (with the same sensitive-logging discipline as the rest of the app), never here.</summary>
        [MaxLength(500)]
        public string? UserSafeFailureMessage { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>Set when the retention job (or a user) deletes the physical file; the row itself is kept
        /// for audit history per the tiered retention rules in SmartCaptureRetentionJobHandler.</summary>
        public DateTime? FileDeletedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
