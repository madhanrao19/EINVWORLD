using System;
using System.Security.Cryptography;
using System.Text;
using eInvWorld.Services.Webhooks;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Verifies the webhook HMAC-SHA256 signature: it matches an independent computation, is deterministic,
    /// carries the <c>sha256=</c> prefix, and changes when the body or secret changes (so a receiver's
    /// verification actually protects integrity/authenticity).
    /// </summary>
    public class WebhookSignerTests
    {
        private static string ExpectedHex(string secret, string body)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        }

        [Fact]
        public void Sign_MatchesIndependentHmac_AndHasPrefix()
        {
            const string secret = "s3cr3t";
            const string body = "{\"invoiceNo\":\"INV-1\",\"status\":\"Valid\"}";

            var sig = WebhookSigner.Sign(secret, body);

            Assert.StartsWith("sha256=", sig);
            Assert.Equal("sha256=" + ExpectedHex(secret, body), sig);
        }

        [Fact]
        public void Sign_IsDeterministic()
        {
            Assert.Equal(WebhookSigner.Sign("k", "payload"), WebhookSigner.Sign("k", "payload"));
        }

        [Fact]
        public void Sign_ChangesWithBody_AndWithSecret()
        {
            var baseline = WebhookSigner.Sign("k", "payload");
            Assert.NotEqual(baseline, WebhookSigner.Sign("k", "payload2"));
            Assert.NotEqual(baseline, WebhookSigner.Sign("k2", "payload"));
        }

        [Fact]
        public void Sign_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => WebhookSigner.Sign(null!, "x"));
            Assert.Throws<ArgumentNullException>(() => WebhookSigner.Sign("x", null!));
        }
    }
}
