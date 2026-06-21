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

    }
}
