using System;
using System.Net;
using Microsoft.AspNetCore.Html;

namespace eInvWorld.Helpers
{
    /// <summary>
    /// The one mapping from an invoice status id (LHDN or internal, see <see cref="eInvWorld.Models.StatusCodes"/>) to its
    /// badge: tone, CSS class, icon and label. Pages render badges through this instead of their own
    /// switch statements, so a status looks the same everywhere and changes colour in one place
    /// (the --einv-status-* tokens in einvworld-tokens.css).
    /// </summary>
    public static class InvoiceStatusBadge
    {
        /// <summary>Tone name used by the <c>einv-status-{tone}</c> class and the status tokens.</summary>
        public static string Tone(string? statusId) => statusId switch
        {
            "Valid" or "Completed" => "valid",
            "Submitted" or "ValidInternal" => "submitted",
            "Invalid" or "TransmissionError" => "invalid",
            "RequestReject" => "rejected",
            "Cancelled" or "CancelledInternal" => "cancelled",
            _ => "draft" // Draft and informational states
        };

        /// <summary>Classes for a badge element (span or button).</summary>
        public static string CssClass(string? statusId) => "einv-badge einv-status-" + Tone(statusId);

        /// <summary>Remix icon class shown before the label, or null for neutral states.</summary>
        public static string? Icon(string? statusId) => Tone(statusId) switch
        {
            "valid" => "ri-checkbox-circle-line",
            "submitted" => "ri-time-line",
            "invalid" or "rejected" => "ri-error-warning-line",
            "cancelled" => "ri-close-circle-line",
            _ => null
        };

        /// <summary>Human label: the <see cref="eInvWorld.Models.StatusCodes"/> description when the id is known, else the id.</summary>
        public static string Label(string? statusId) =>
            Enum.TryParse<eInvWorld.Models.StatusCodes>(statusId, out var code) ? EnumHelper.GetEnumDescription(code) : statusId ?? string.Empty;

        /// <summary>
        /// A complete badge <c>&lt;span&gt;</c>. <paramref name="label"/> overrides the default label
        /// (e.g. a page's own display name); it is HTML-encoded.
        /// </summary>
        public static IHtmlContent Render(string? statusId, string? label = null)
        {
            // No status yet (e.g. a draft's LHDN status): a quiet dash, not an empty pill.
            if (string.IsNullOrEmpty(statusId) && string.IsNullOrEmpty(label))
                return new HtmlString("<span class=\"text-muted\" aria-label=\"No status yet\">&mdash;</span>");

            var icon = Icon(statusId);
            var text = WebUtility.HtmlEncode(label ?? Label(statusId));
            var iconHtml = icon == null ? string.Empty : $"<i class=\"{icon}\" aria-hidden=\"true\"></i>";
            return new HtmlString($"<span class=\"{CssClass(statusId)}\">{iconHtml}{text}</span>");
        }
    }
}
