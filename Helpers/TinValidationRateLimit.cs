using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Per-user rate limit for the "Validate with LHDN" POST handlers (<c>ValidateTIN</c>,
    /// <c>ValidateShippingRecipientTin</c>): each call spends an LHDN taxpayer-validation request.
    /// Applied at page level (Razor Pages ignores rate-limit attributes on handler methods), so
    /// <see cref="Applies"/> limits only those handlers and leaves the rest of the page alone.
    /// </summary>
    public static class TinValidationRateLimit
    {
        /// <summary>Rate-limiter policy name (registered in Program.cs).</summary>
        public const string Policy = "lhdn-validate";

        /// <summary>True for a POST to a <c>Validate…Tin</c> page handler.</summary>
        public static bool Applies(HttpContext context)
        {
            var handler = PostHandler(context);
            return handler != null
                && handler.StartsWith("Validate", StringComparison.OrdinalIgnoreCase)
                && handler.EndsWith("Tin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The Razor Pages handler a POST will run, or null for any other method.</summary>
        public static string? PostHandler(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method)) return null;
            // Same resolution as Razor Pages' handler selector: the route value, else the FIRST "handler" query value,
            // so "?handler=ValidateTIN&handler=x" (which still runs ValidateTIN) cannot slip past the limit.
            var handler = Convert.ToString(context.GetRouteValue("handler"));
            if (string.IsNullOrEmpty(handler) && context.Request.Query.TryGetValue("handler", out var values) && values.Count > 0)
                handler = values[0];
            return string.IsNullOrEmpty(handler) ? null : handler;
        }
    }

    /// <summary>
    /// Per-user rate limit for "Send to buyers" (<c>BulkSendToBuyers</c> on the Invoice List): every request emails real
    /// customers. Page-level like <see cref="TinValidationRateLimit"/>, limiting only that handler.
    /// Configurable via <c>RateLimiting:SendToBuyersPerMinute</c> (default 5).
    /// </summary>
    public static class SendToBuyersRateLimit
    {
        /// <summary>Rate-limiter policy name (registered in Program.cs).</summary>
        public const string Policy = "send-to-buyers";

        /// <summary>True for a POST to the <c>BulkSendToBuyers</c> handler.</summary>
        public static bool Applies(HttpContext context) =>
            string.Equals(TinValidationRateLimit.PostHandler(context), "BulkSendToBuyers", StringComparison.OrdinalIgnoreCase);
    }
}
