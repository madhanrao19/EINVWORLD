using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    public class ItemDescription
    {
        public int Id { get; set; }
        public string ClassificationCode { get; set; } = null!;
        public string ItemCode { get; set; } = null!;
        public string Description { get; set; } = null!;

        /// <summary>LHDN unit-of-measure code (references <see cref="eInvWorld.Models.UnitType"/>). Optional —
        /// existing catalogue rows and invoice lines are unaffected; a line's own Unit always wins.</summary>
        public string? UnitCode { get; set; }

        /// <summary>Catalogue default price, used only to prefill a new invoice line. Never rewrites a
        /// historical invoice line's own UnitPrice — that stays exactly as submitted/saved.</summary>
        [Column(TypeName = "decimal(18,4)")]
        [Range(0, 999999999999.9999, ErrorMessage = "Unit Price must be zero or a positive amount.")]
        public decimal? UnitPrice { get; set; }

        public int? CreatedByCompanyId { get; set; }
        public bool IsActive { get; set; } = true;

        // Add these new properties
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}