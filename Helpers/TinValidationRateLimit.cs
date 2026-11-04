using System;
using Microsoft.AspNetCore.Http;

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
        public static bool Applies(HttpRequest request)
        {
            var handler = request.Query["handler"].ToString();
            return HttpMethods.IsPost(request.Method)
                && handler.StartsWith("Validate", StringComparison.OrdinalIgnoreCase)
                && handler.EndsWith("Tin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
