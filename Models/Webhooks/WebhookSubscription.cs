using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Webhooks
{
    /// <summary>
    /// A per-company (TIN) HTTP callback registration. When one of the company's invoices reaches a
    /// terminal LHDN status, the background dispatcher enqueues a durable <c>WebhookDelivery</c> job that
    /// POSTs a signed JSON payload to <see cref="CallbackUrl"/>. Receivers verify authenticity with the
    /// HMAC-SHA256 signature computed from <see cref="Secret"/> (see the webhook docs).
    /// </summary>
    public class WebhookSubscription
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The company TIN whose invoices trigger this webhook.</summary>
        [Required]
        [MaxLength(50)]
        public string Tin { get; set; } = string.Empty;

        /// <summary>Absolute HTTP(S) URL the payload is POSTed to.</summary>
        [Required]
        [MaxLength(2048)]
        public string CallbackUrl { get; set; } = string.Empty;

        /// <summary>
        /// The HMAC signing secret shared with the receiver. Encrypted at rest via the DataProtection
        /// key-ring (EF value converter); the column is widened to <c>nvarchar(max)</c> to hold ciphertext.
        /// </summary>
        [Required]
        public string Secret { get; set; } = string.Empty;

        /// <summary>Optional human-readable label shown in the Admin UI.</summary>
        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>When false, the subscription is skipped by both the scanner and the delivery handler.</summary>
        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; }

        [MaxLength(256)]
        public string? CreatedBy { get; set; }

        /// <summary>Timestamp of the most recent delivery attempt (success or failure).</summary>
        public DateTime? LastDeliveryAtUtc { get; set; }

        /// <summary>Short outcome of the most recent delivery attempt, e.g. "200 OK" or "Failed: timeout".</summary>
        [MaxLength(500)]
        public string? LastDeliveryResult { get; set; }
    }
}
