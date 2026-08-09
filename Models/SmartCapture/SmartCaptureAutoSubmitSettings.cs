using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Models.SmartCapture
{
    /// <summary>
    /// Smart Capture Stage 4 (reduced first cut): a company's explicit opt-in into automatic LHDN
    /// submission of Smart Capture drafts that pass every deterministic gate in
    /// SmartCaptureAutoSubmitEligibilityService. Defaults to OFF (Enabled=false); managed only from the
    /// system Admin area (Pages/Admin/SmartCaptureAutoSubmit), never self-service by a company's own
    /// Supplier user — a company cannot turn on unattended submission to a government tax authority for
    /// itself without EINVWORLD operator involvement. One row per company; absence of a row means the
    /// company has never been opted in (equivalent to Enabled=false).
    /// </summary>
    public class SmartCaptureAutoSubmitSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CompanyPartyInfoId { get; set; }

        [ForeignKey(nameof(CompanyPartyInfoId))]
        public PartyInfo? CompanyPartyInfo { get; set; }

        public bool Enabled { get; set; }

        /// <summary>Comma-separated LHDN document type codes eligible for auto-submit (e.g. "01"). Any
        /// confirmed doc type not in this list always falls back to manual submission, regardless of
        /// every other condition passing.</summary>
        [MaxLength(80)]
        public string AllowedDocTypesCsv { get; set; } = "01";

        /// <summary>Invoices at or above this total payable amount always require manual submission,
        /// regardless of every other condition passing. Required (&gt; 0) whenever Enabled is true — there
        /// is no "unlimited" auto-submit tier in this first cut.</summary>
        public decimal MaxAutoSubmitValue { get; set; }

        /// <summary>Minutes between draft confirmation and the actual LHDN submission attempt — the
        /// window during which the pending auto-submission is visible and cancellable on the Smart
        /// Capture list page.</summary>
        public int DelayMinutes { get; set; } = 20;

        public DateTime UpdatedAtUtc { get; set; }

        [MaxLength(450)]
        public string? UpdatedByUserId { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
