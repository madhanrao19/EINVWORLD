using System;
using System.Security.Cryptography;
using System.Text;

namespace eInvWorld.Services.Webhooks
{
    /// <summary>
    /// Computes the HMAC-SHA256 signature a receiver uses to verify a webhook payload's authenticity and
    /// integrity. The signature is the lower-case hex of <c>HMAC_SHA256(secret, rawJsonBody)</c>, sent in
    /// the <c>X-EInvWorld-Signature</c> header as <c>sha256=&lt;hex&gt;</c> (the widely-used convention).
    /// </summary>
    public static class WebhookSigner
    {
        public const string SignaturePrefix = "sha256=";

        /// <summary>Returns <c>sha256=&lt;hex&gt;</c> for the given raw body and secret.</summary>
        public static string Sign(string secret, string payload)
        {
            if (secret is null) throw new ArgumentNullException(nameof(secret));
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return SignaturePrefix + Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
