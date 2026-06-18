using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Recurring
{
    public class RecurringRunHistory
    {
        [Key]
        public int Id { get; set; }

        public int RecurringProfileId { get; set; }

        public DateTime RunTimestamp { get; set; }

        // e.g., Success_Draft, Success_Submitted, LHDN_Failed, Internal_Error
        [MaxLength(50)]
        public string RunStatus { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? GeneratedInvoiceNo { get; set; }

        [MaxLength(100)]
        public string? LhdnSubmissionUid { get; set; }

        public string? ErrorMessage { get; set; }
    }
}