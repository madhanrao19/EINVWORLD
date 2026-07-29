using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace EINVWORLD.Services
{
    /// <summary>
    /// Single client-side rate limiter that keeps the app safely below the MyInvois/LHDN
    /// per-API request limits. One token-bucket per endpoint class, each capped a little under
    /// the official RPM so we never trip the server-side 429 in normal operation.
    ///
    /// Registered as a DI singleton (see Program.cs) so the SAME limiter instances back every
    /// HttpClient built from it — including across IHttpClientFactory's periodic handler-pool
    /// rotation (default every 2 minutes), which would otherwise silently reset the buckets.
    /// On a SINGLE-INSTANCE deployment this is correct and safe: a global cap that is &lt;= the
    /// per-TIN limit can never exceed any single TIN's limit. If this app is ever scaled to
    /// multiple instances behind a load balancer, this must move to a shared/distributed limiter
    /// (e.g. Redis) — see PRODUCTION-READINESS-REVIEW.md §2.6.
    ///
    /// Per-minute limits are configurable via LHDNApiConfig:RateLimits:* (see appsettings.json) —
    /// the SDK's official limits differ between preprod/sandbox and production, so an environment
    /// can override any of them without a code change. Values below are the defaults when a key
    /// isn't set, matched to the current SDK's preprod limits: token 12 · validate 60 · submit 100 ·
    /// get-submission 300 · search/recent 12 · get-document 60 · cancel/reject 12.
    ///
    /// IMPORTANT — requests are PACED EVENLY, not bursted. A token bucket sized at the full per-minute
    /// limit (e.g. 50 tokens) lets 50 requests fire in one instant; LHDN enforces a tighter window and
    /// replies 429 ("try again in 59 seconds"), which then stalls the whole sync. Per the MyInvois SDK
    /// guidance we instead release ONE request every (60s / rate) with only a tiny burst — staying under
    /// the limit at all times so 429s do not occur. Excess requests queue (and wait) rather than fail.
    /// </summary>
    public class LhdnRateLimitHandler : DelegatingHandler
    {
        private readonly RateLimiter _token;
        private readonly RateLimiter _validate;
        private readonly RateLimiter _submit;
        private readonly RateLimiter _poll;
        private readonly RateLimiter _search;
        private readonly RateLimiter _getDoc;
        private readonly RateLimiter _state;
        private readonly RateLimiter _general;

        public LhdnRateLimitHandler(IConfiguration configuration)
        {
            const string section = "LHDNApiConfig:RateLimits:";
            int PerMinute(string key, int fallback) => configuration.GetValue($"{section}{key}", fallback);

            // (perMinute, burst): sustained ≈ perMinute, max instantaneous burst = burst. Burst sizes
            // are an internal pacing detail (not an LHDN-published limit), so they stay fixed.
            _token = PacedBucket(PerMinute("TokenPerMinute", 10), burst: 1);       // official 12  (/connect/token)
            _validate = PacedBucket(PerMinute("ValidatePerMinute", 50), burst: 2); // official 60  (/taxpayer/validate)
            _submit = PacedBucket(PerMinute("SubmitPerMinute", 60), burst: 3);     // official 100 (POST /documentsubmissions)
            _poll = PacedBucket(PerMinute("PollPerMinute", 200), burst: 5);        // official 300 (GET  /documentsubmissions/{id})
            _search = PacedBucket(PerMinute("SearchPerMinute", 10), burst: 1);     // official 12  (/documents/search)
            _getDoc = PacedBucket(PerMinute("GetDocPerMinute", 50), burst: 2);     // official ~60 (/documents/{uuid}/raw) — main 429 source
            _state = PacedBucket(PerMinute("StatePerMinute", 10), burst: 1);       // official 12  (PUT /documents/state/{id}/state)
            _general = PacedBucket(PerMinute("GeneralPerMinute", 20), burst: 2);   // fallback for anything else
        }

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

        private RateLimiter SelectLimiter(HttpRequestMessage request)
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
