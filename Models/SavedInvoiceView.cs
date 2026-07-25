using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    /// <summary>
    /// A user's named preset of filters for the invoice list (Stitch "Saved Views" — e.g. "My Pending",
    /// "High Value"). Stores the exact query string the filter form already produces, so applying a view
    /// is just navigating to <c>./InvoiceLists?{QueryString}</c> — no separate filter-serialization logic
    /// to keep in sync with the real filter fields.
    /// </summary>
    public class SavedInvoiceView
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)] // matches ASP.NET Core Identity's default Id column width
        public string UserId { get; set; } = string.Empty;

        /// <summary>Which tab this view belongs to ("All", "Draft", "Sent", "Received") — a view saved on
        /// one tab only ever appears on that tab, since the underlying filter fields differ per tab.</summary>
        [Required]
        [MaxLength(20)]
        public string InvoiceDirection { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>The filter form's query string (without the leading '?'), e.g.
        /// "LHDNStatus=Submitted&amp;amountMin=1000". Direction/pageNo/sortBy are intentionally excluded —
        /// those come from the tab and its live sort state, not the saved preset.</summary>
        [Required]
        [MaxLength(2048)]
        public string QueryString { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
