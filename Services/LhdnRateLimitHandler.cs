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
    /// </summary>
    public class LhdnRateLimitHandler : DelegatingHandler
    {
        private static readonly RateLimiter _token    = Bucket(10);   // official 12  (/connect/token)
        private static readonly RateLimiter _validate = Bucket(50);   // official 60  (/taxpayer/validate)
        private static readonly RateLimiter _submit   = Bucket(85);   // official 100 (POST /documentsubmissions)
        private static readonly RateLimiter _poll     = Bucket(240);  // official 300 (GET  /documentsubmissions/{id})
        private static readonly RateLimiter _search   = Bucket(8);    // official 12  (/documents/search)
        private static readonly RateLimiter _getDoc   = Bucket(50);   // official ~60 (/documents/{uuid}/raw and other doc reads)
        private static readonly RateLimiter _state    = Bucket(10);   // official 12  (PUT /documents/state/{id}/state)
        private static readonly RateLimiter _general  = Bucket(30);   // fallback for anything else

        private static RateLimiter Bucket(int perMinute) => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = perMinute,
            TokensPerPeriod = perMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 1000,
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
