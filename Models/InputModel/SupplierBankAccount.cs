using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    /// <summary>
    /// One of a company's (supplier's) bank accounts. A company may hold several; exactly one is the default,
    /// pre-selected on new invoices. <see cref="PartyInfo.BankName"/>/<see cref="PartyInfo.BankAccountNo"/> stay as
    /// a mirror of the default account (kept in sync by <c>SupplierBankAccountService</c>) so the PDF, CSV and
    /// LHDN paths that read them keep working. Invoices still store their own snapshot of the chosen account.
    /// </summary>
    public class SupplierBankAccount
    {
        [Key]
        public int Id { get; set; }

        public int PartyInfoId { get; set; }

        [ForeignKey(nameof(PartyInfoId))]
        public PartyInfo? PartyInfo { get; set; }

        [Required]
        [Display(Name = "Bank Name")]
        [StringLength(100, ErrorMessage = "Bank Name cannot exceed 100 characters.")]
        public string BankName { get; set; } = string.Empty;

        /// <summary>Encrypted at rest (ApplicationDbContext.Encrypt); the length limit applies to the plaintext.</summary>
        [Required]
        [Display(Name = "Bank Account Number")]
        [StringLength(150, ErrorMessage = "Bank Account Number cannot exceed 150 characters.")]
        public string BankAccountNo { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public DateTime CreatedDate { get; set; }

        [StringLength(256)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        [StringLength(256)]
        public string? UpdatedBy { get; set; }
    }
}
