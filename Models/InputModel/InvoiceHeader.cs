using eInvWorld.Models.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    public class InvoiceHeader
    {
        [Key]
        [Required]
        [MaxLength(50)]
        public string InvoiceNo { get; set; } = null!;  // Invoice Number

        public string PrefixedID { get; set; } = null!;  // Prefixed ID
        public string? RefDocumentNo { get; set; }  // Reference Document Number

        [Required]
        public DateTime CreatedDate { get; set; }  // Creation Date (for auditing)

        public DateTime? IssueDate { get; set; }  // Issue Date

        public string DocTypeCode { get; set; } = null!;  // Document Type Code

        /// <summary>
        /// e-Invoice Special Voluntary Disclosure Programme flag (LHDN SDK, 8 Jul 2026; programme runs
        /// until 31 Dec 2027). When set, the document is submitted as version 1.2 (SVDP, unsigned)
        /// instead of 1.0. SVDP 1.3 (signed) additionally requires LHDNApiConfig:SigningEnabled + a cert.
        /// </summary>
        public bool IsSvdp { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = null!;  // Currency Code (e.g., "MYR", "USD")

        public string? ForeignCurrency { get; set; }  // Foreign Currency (if any)
        public decimal? ExchangeRate { get; set; }  // Exchange Rate (if any)

        // Relationships with Suppliers and Customers
        [ForeignKey("Supplier")]
        public int? SupplierId { get; set; }  // Nullable Foreign Key to Supplier
        [ForeignKey("Customer")]
        public int? CustomerId { get; set; }  // Nullable Foreign Key to Customer

        // ADD: New Link for PublicCustomer
        [ForeignKey("PublicCustomer")]
        public int? PublicCustomerId { get; set; }
        public virtual PublicCustomer? PublicCustomer { get; set; }

        public virtual PartyInfo Supplier { get; set; } = null!;  // Navigation property for Supplier
        public virtual PartyInfo Customer { get; set; } = null!;  // Navigation property for Customer

        // ADD UUID & SUBMISSION ID HERE
        [MaxLength(100)]
        public string? UUID { get; set; }  // Unique Invoice ID from LHDN API

        [MaxLength(100)]
        public string? SubmissionID { get; set; }  // Submission ID from LHDN API

        [MaxLength(100)]
        public string? RefUUID { get; set; }  // Reference UUID from the original invoice (for CNs)


        // Aggregated Totals
        public decimal? TotalAmountIncTax { get; set; }  // Total Amount including Tax
        public decimal? TotalTaxAmount { get; set; }  // Total Tax Amount
        public decimal? TotalDiscountAmount { get; set; }  // Total Discount Amount
        public decimal? TotalAmountExclTax { get; set; }  // Total Amount excluding Tax //LineExtensionAmount
        public decimal? TotalPayableAmount { get; set; }  // Total Amount Payable
        public decimal? TotalNetAmount { get; set; }  // Total Net Amount

        public DateTime? StartDate { get; set; }  // Start Date (if applicable)
        public DateTime? EndDate { get; set; }  // End Date (if applicable)

        // Status Tracking
        [Required]
        public string InternalStatusId { get; set; } = null!;  // Link to Status table for internal status
        [ForeignKey("InternalStatusId")]
        public virtual Status InternalStatus { get; set; } = null!;

        public string? LHDNStatusId { get; set; }  // Optional: Link to LHDN status
        [ForeignKey("LHDNStatusId")]
        public virtual Status LHDNStatus { get; set; } = null!;

        // Navigation Properties
        public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();  // Related invoice lines
        public virtual ICollection<AllowanceCharge> AllowanceCharges { get; set; } = new List<AllowanceCharge>();


        // Invoice Period
        public InvoicePeriodEnum? InvoicePeriod { get; set; }

        // Delivery Party Information
        public DeliveryParty DeliveryParty { get; set; } = null!;

        // Additional Auditing Fields
        [MaxLength(50)]
        public string CreatedBy { get; set; } = null!;  // User who created the invoice

        [MaxLength(50)]
        public string? UpdatedBy { get; set; }  // User who last updated the invoice

        public DateTime? LastUpdated { get; set; }  // Last update timestamp

        // Notes for additional information
        [MaxLength(500)]
        public string? Notes { get; set; }

        [NotMapped]
        public bool IsSent => SupplierId != null;  // If SupplierId exists, it's a Sent Invoice

        [NotMapped]
        public bool IsReceived => CustomerId != null || PublicCustomerId != null;


        //Reject Details
        public string? RejectedReason { get; set; }
        [MaxLength(50)]
        public string? RejectedBy { get; set; }
        public DateTime? RejectedTimestamp { get; set; }
        public string? InvoiceDirection { get; set; }
        public string? LHDNValidationErrorJson { get; set; }

        // ✅ New Fields from API Response
        public string? LongId { get; set; }  // ✅ Used for QR code generation
		
		public DateTime? DateTimeReceived { get; set; }  // ✅ Timestamp when document was submitted
        public DateTime? DateTimeValidated { get; set; }  // ✅ Timestamp when document became valid
        public DateTime? CancelDateTime { get; set; }
        // Persisted so a background retry of the cancellation email (IsCancellationEmailSent below)
        // can rebuild the email body without the original interactive request's in-memory reason.
        public string? CancellationReason { get; set; }

        // Concurrency claim for submission: set atomically just before a submit to LHDN so two
        // simultaneous requests cannot both post the same document. Cleared on failure; a claim older
        // than a few minutes is treated as stale (e.g. a crashed submit) and may be reclaimed.
        public DateTime? SubmissionClaimedAtUtc { get; set; }

		public bool IsValidationEmailSent { get; set; } = false;
        [MaxLength(500)]
        public string? ValidationEmailSentTo { get; set; }
        public DateTime? ValidationEmailSentAt { get; set; }

        public bool IsPdfGenerated { get; set; } = false;
        public DateTime? PdfGeneratedAt { get; set; }

        // "New e-invoice received" notification (buyer-side, for invoices synced in from LHDN that an
        // external ERP submitted directly). Defaults to true ("not applicable") so every other invoice
        // creation path in the app — normal Sent-invoice submission, Credit/Debit notes, etc. — is
        // automatically exempt without touching those files; InvoiceFullSyncHelper explicitly sets this
        // to false only for a genuinely new, buyer-side synced invoice, opting it into the retry pass
        // in InvoiceFinalizer/InvoiceStatusUpdater (same atomic-claim-and-rollback pattern as the
        // ValidationEmailSent fields above).
        public bool IsNewInvoiceReceivedEmailSent { get; set; } = true;
        [MaxLength(500)]
        public string? NewInvoiceReceivedEmailSentTo { get; set; }
        public DateTime? NewInvoiceReceivedEmailSentAt { get; set; }

        // Rejection/Cancellation notification emails. Same "true = not applicable" default as
        // IsNewInvoiceReceivedEmailSent above — every invoice is exempt until it is actually
        // rejected/cancelled, at which point the handler that performs that transition sets the
        // flag to false in the SAME save as the status change, opting it into the atomic-claim/
        // retry pass (InvoiceFinalizer/InvoiceStatusUpdater). The interactive handler still attempts
        // the send immediately afterwards for a snappy, MyInvois-like "notified right away"
        // experience; this flag is the safety net for when that immediate attempt fails (SMTP down,
        // misconfiguration, a locked PDF file, etc.) so the notification is never silently lost.
        public bool IsRejectionEmailSent { get; set; } = true;
        [MaxLength(500)]
        public string? RejectionEmailSentTo { get; set; }
        public DateTime? RejectionEmailSentAt { get; set; }

        public bool IsCancellationEmailSent { get; set; } = true;
        [MaxLength(500)]
        public string? CancellationEmailSentTo { get; set; }
        public DateTime? CancellationEmailSentAt { get; set; }

        /// <summary>
        /// The last terminal LHDN status for which an outbound webhook was enqueued. Used by the webhook
        /// dispatcher to fire exactly once per status transition (e.g. Valid → later Cancelled fires twice,
        /// re-scanning the same Valid invoice does not). Null until the first webhook is enqueued.
        /// </summary>
        [MaxLength(20)]
        public string? WebhookNotifiedStatus { get; set; }

        [Display(Name = "Bank Account Number")]
        [StringLength(150, ErrorMessage = "Bank Account Number cannot exceed 150 characters.")]
        public string? BankAccountNo { get; set; }

        [Display(Name = "Bank Name")]
        [StringLength(100, ErrorMessage = "Bank Name cannot exceed 100 characters.")]
        public string? BankName { get; set; }

        [Display(Name = "Attention To")]
        [StringLength(200, ErrorMessage = "Attention To cannot exceed 200 characters.")]
        public string? Attention { get; set; }

        public DateTime? OriginalInvoiceDate { get; set; }

        [MaxLength(100)]
        public string? PoDoNo { get; set; }

        [MaxLength(300)]
        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; }

        [Display(Name = "Incoterms")]
        [StringLength(3, ErrorMessage = "Incoterms must be exactly 3 characters (e.g., FOB, CIF).")]
        public string? Incoterms { get; set; }

        [StringLength(150, ErrorMessage = "Prepayment Reference Number cannot exceed 150 characters.")] // NEW
        public string? PrepaymentReferenceNumber { get; set; }

        // Shipping Recipient — applicable only when goods ship to a different recipient/address than
        // the Buyer's own. All optional; a redesigned invoice-level "Additional Information" section
        // surfaces these behind progressive disclosure.
        [Display(Name = "Shipping Recipient's Name")]
        [StringLength(200, ErrorMessage = "Shipping Recipient's Name cannot exceed 200 characters.")]
        public string? ShippingRecipientName { get; set; }

        [StringLength(200, ErrorMessage = "Address Line 1 cannot exceed 200 characters.")]
        public string? ShippingRecipientAddrLine1 { get; set; }

        [StringLength(200, ErrorMessage = "Address Line 2 cannot exceed 200 characters.")]
        public string? ShippingRecipientAddrLine2 { get; set; }

        [StringLength(200, ErrorMessage = "Address Line 3 cannot exceed 200 characters.")]
        public string? ShippingRecipientAddrLine3 { get; set; }

        [StringLength(20, ErrorMessage = "Postcode cannot exceed 20 characters.")]
        public string? ShippingRecipientPostcode { get; set; }

        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
        public string? ShippingRecipientCity { get; set; }

        // Free text, not a StateCodes FK — matches the existing precedent for a foreign party's
        // State/Province (self-imposed FK removed in v1.21.7; a shipping recipient can be anywhere).
        [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
        public string? ShippingRecipientState { get; set; }

        [StringLength(3, ErrorMessage = "Country must be a 3-letter ISO code.")]
        public string? ShippingRecipientCountryCode { get; set; }

        [ForeignKey("ShippingRecipientCountryCode")]
        public virtual CountryCode? ShippingRecipientCountry { get; set; }

        [StringLength(10, ErrorMessage = "ID Type cannot exceed 10 characters.")]
        public string? ShippingRecipientIdType { get; set; }

        [ForeignKey("ShippingRecipientIdType")]
        public virtual RegistrationType? ShippingRecipientIdTypeRef { get; set; }

        [StringLength(150, ErrorMessage = "Registration/Identification/Passport Number cannot exceed 150 characters.")]
        public string? ShippingRecipientIdNumber { get; set; }

        [Display(Name = "Shipping Recipient's TIN")]
        [StringLength(20, ErrorMessage = "TIN cannot exceed 20 characters.")]
        public string? ShippingRecipientTIN { get; set; }

        // Import/Export (Customs) information — applicable only to import/export of goods.
        [Display(Name = "Reference Number of Customs Form No.1, 9 etc.")]
        [StringLength(500, ErrorMessage = "Customs Form No.1 reference cannot exceed 500 characters.")]
        public string? CustomsFormNo1Reference { get; set; }

        [Display(Name = "Free Trade Agreement (FTA) Information")]
        [StringLength(200, ErrorMessage = "Free Trade Agreement information cannot exceed 200 characters.")]
        public string? FreeTradeAgreementInfo { get; set; }

        [Display(Name = "Authorization Number for Certified Exporter")]
        [StringLength(100, ErrorMessage = "Authorization Number cannot exceed 100 characters.")]
        public string? CertifiedExporterAuthorizationNumber { get; set; }

        [Display(Name = "Reference Number of Custom Form No.2")]
        [StringLength(500, ErrorMessage = "Customs Form No.2 reference cannot exceed 500 characters.")]
        public string? CustomsFormNo2Reference { get; set; }

        [Display(Name = "Details of Other Charges")]
        public decimal? OtherChargesAmount { get; set; }

        [StringLength(500, ErrorMessage = "Details of Other Charges Description cannot exceed 500 characters.")]
        public string? OtherChargesDescription { get; set; }

        /// <summary>
        /// SQL Server rowversion concurrency token. Guards against lost updates when the background
        /// status sync and a user action (cancel/edit) write the same invoice concurrently: the later
        /// SaveChanges throws <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
        /// instead of silently overwriting. Background sync reloads and lets the next poll re-sync;
        /// the user cancel path reapplies and retries (LHDN has already accepted the cancellation).
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

    }
}
