using System.Net;
using System.Threading.RateLimiting;

namespace EINVWORLD.Services
{
    /// <summary>
    /// Single client-side rate limiter that keeps the app safely below the MyInvois/LHDN
    /// per-API request limits. One token-bucket per endpoint class, each capped a little under
    /// the official RPM so we never trip the server-side 429 in normal operation.
    ///
    /// The buckets are process-wide (static). On a SINGLE-INSTANCE deployment this is correct and
    /// safe: a global cap that is &lt;= the per-TIN limit can never exceed any single TIN's limit.
    /// If this app is ever scaled to multiple instances behind a load balancer, this must move to a
    /// shared/distributed limiter (e.g. Redis) — see PRODUCTION-READINESS-REVIEW.md §2.6.
    ///
    /// Official limits (verify against the current MyInvois SDK portal — they change and differ
    /// between preprod/prod): token 12 · validate 60 · submit 100 · get-submission 300 ·
    /// search/recent 12 · get-document 60 · cancel/reject 12.
    ///
    /// IMPORTANT — requests are PACED EVENLY, not bursted. A token bucket sized at the full per-minute
    /// limit (e.g. 50 tokens) lets 50 requests fire in one instant; LHDN enforces a tighter window and
    /// replies 429 ("try again in 59 seconds"), which then stalls the whole sync. Per the MyInvois SDK
    /// guidance we instead release ONE request every (60s / rate) with only a tiny burst — staying under
    /// the limit at all times so 429s do not occur. Excess requests queue (and wait) rather than fail.
    /// </summary>
    public class LhdnRateLimitHandler : DelegatingHandler
    {
        // PacedBucket(perMinute, burst): sustained ≈ perMinute, max instantaneous burst = burst.
        private static readonly RateLimiter _token    = PacedBucket(10,  burst: 1);  // official 12  (/connect/token)
        private static readonly RateLimiter _validate = PacedBucket(50,  burst: 2);  // official 60  (/taxpayer/validate)
        private static readonly RateLimiter _submit   = PacedBucket(60,  burst: 3);  // official 100 (POST /documentsubmissions)
        private static readonly RateLimiter _poll     = PacedBucket(200, burst: 5);  // official 300 (GET  /documentsubmissions/{id})
        private static readonly RateLimiter _search   = PacedBucket(10,  burst: 1);  // official 12  (/documents/search)
        private static readonly RateLimiter _getDoc   = PacedBucket(50,  burst: 2);  // official ~60 (/documents/{uuid}/raw) — main 429 source
        private static readonly RateLimiter _state    = PacedBucket(10,  burst: 1);  // official 12  (PUT /documents/state/{id}/state)
        private static readonly RateLimiter _general  = PacedBucket(20,  burst: 2);  // fallback for anything else

        private static RateLimiter PacedBucket(int perMinute, int burst) => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,                                                   // small burst, not the full minute's worth
            TokensPerPeriod = 1,                                                  // release one at a time…
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(60000.0 / perMinute), // …every (60s / rate) → even pacing
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5000,                                                    // queue (wait) rather than drop, since callers can wait
            AutoReplenishment = true
        });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var limiter = SelectLimiter(request);

            using var lease = await limiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                // Our internal queue is saturated. Surface a 429 so the existing retry/back-off logic
                // in LHDNApiService / TokenService waits instead of hammering LHDN.
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    ReasonPhrase = "Client-side rate limit (queue full)"
                };
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private static RateLimiter SelectLimiter(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath.ToLowerInvariant() ?? string.Empty;
            var method = request.Method;

            // Order matters: check the more specific paths first.
            if (path.Contains("/connect/token")) return _token;
            if (path.Contains("/taxpayer/validate")) return _validate;
            if (path.Contains("/documentsubmissions"))
                return method == HttpMethod.Post ? _submit : _poll;
            if (path.Contains("/documents/state/")) return _state;
            if (path.Contains("/documents/search")) return _search;
            if (path.Contains("/documents/")) return _getDoc; // /documents/{uuid}/raw and other reads

            return _general;
        }
    }
}
