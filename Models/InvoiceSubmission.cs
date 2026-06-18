using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models
{
    public class InvoiceSubmission
    {
        [Key]
        public int SubmissionId { get; set; }  // Primary Key

        [Required]
        public string InvoiceNo { get; set; } = null!;  // Links to InvoiceHeader

        [Required]
        public string InternalStatusId { get; set; } = null!;  // Foreign Key for internal status
        [ForeignKey("InternalStatusId")]
        public virtual Status InternalStatus { get; set; } = null!;

        [Required]
        public string LHDNStatusId { get; set; } = null!;  // Foreign Key for LHDN status
        [ForeignKey("LHDNStatusId")]
        public virtual Status LHDNStatus { get; set; } = null!;

        [Required]
        public DateTime SubmissionDate { get; set; }  // Date when the submission was created

        public DateTime? LastUpdated { get; set; }  // Last update date
        [Required]
        [MaxLength(50)]
        public string SubmittedBy { get; set; } = null!;  // User who submitted the invoice

        [MaxLength(50)]
        public string UpdatedBy { get; set; } = null!;  // User who last updated the submission

        [MaxLength(500)]
        public string Notes { get; set; } = null!;  // Notes or metadata
    }
}
