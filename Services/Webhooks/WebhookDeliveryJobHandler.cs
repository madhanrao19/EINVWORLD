using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Webhooks;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services.Webhooks
{
    /// <summary>
    /// Delivers one outbound webhook (<see cref="SyncJobType.WebhookDelivery"/>): builds the signed JSON
    /// payload and POSTs it to the subscription's callback URL. Throws on transport failure or a non-2xx
    /// response so the durable queue retries with backoff and dead-letters after the max attempts. A
    /// removed/disabled subscription is a no-op success (nothing to deliver).
    /// </summary>
    public sealed class WebhookDeliveryJobHandler : ISyncJobHandler
    {
        /// <summary>Named <see cref="HttpClient"/> registered in Program.cs.</summary>
        public const string HttpClientName = "webhook";
        public const string EventType = "invoice.status";

        private static readonly JsonSerializerOptions PayloadOpts = new()
        {
            // Stable, compact body — the exact bytes we sign must be the exact bytes we send.
            WriteIndented = false
        };

        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly WebhookSettings _settings;
        private readonly ILogger<WebhookDeliveryJobHandler> _logger;

        public WebhookDeliveryJobHandler(
            ApplicationDbContext db,
            IHttpClientFactory httpFactory,
            IOptions<WebhookSettings> settings,
            ILogger<WebhookDeliveryJobHandler> logger)
        {
            _db = db;
            _httpFactory = httpFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public string JobType => SyncJobType.WebhookDelivery;

        public async Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var payload = SyncJobPayload.Parse(job.PayloadJson)
                ?? throw new InvalidOperationException("WebhookDelivery job has an unparsable payload.");
            if (payload.WebhookSubscriptionId is not int subId || string.IsNullOrWhiteSpace(payload.InvoiceNo))
                throw new InvalidOperationException("WebhookDelivery job is missing subscription id or invoice number.");

            var sub = await _db.Set<WebhookSubscription>().FirstOrDefaultAsync(s => s.Id == subId, ct);
            if (sub is null)
                return $"Subscription {subId} no longer exists — nothing to deliver.";
            if (!sub.IsEnabled)
                return $"Subscription {subId} is disabled — delivery skipped.";

            ValidateCallbackUrl(sub.CallbackUrl); // throws on a disallowed/SSRF URL (permanent config error)

            var body = BuildBody(payload, job.Tin);
            var signature = WebhookSigner.Sign(sub.Secret, body);

            using var request = new HttpRequestMessage(HttpMethod.Post, sub.CallbackUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-EInvWorld-Event", EventType);
            request.Headers.TryAddWithoutValidation("X-EInvWorld-Signature", signature);
            request.Headers.TryAddWithoutValidation("X-EInvWorld-Delivery", job.Id.ToString());

            var client = _httpFactory.CreateClient(HttpClientName);

            string outcome;
            try
            {
                using var response = await client.SendAsync(request, ct);
                outcome = $"{(int)response.StatusCode} {response.StatusCode}";
                await RecordAttemptAsync(sub, outcome, ct);

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Webhook to subscription {subId} returned {outcome} for invoice {payload.InvoiceNo}.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                outcome = $"Failed: {ex.GetType().Name}";
                await RecordAttemptAsync(sub, outcome, ct);
                throw new InvalidOperationException(
                    $"Webhook to subscription {subId} failed for invoice {payload.InvoiceNo}: {ex.Message}", ex);
            }

            return $"Delivered {payload.Status} for {payload.InvoiceNo} to subscription {subId}: {outcome}.";
        }

        /// <summary>Serializes the payload the receiver gets (and that the signature covers).</summary>
        private static string BuildBody(SyncJobPayload payload, string tin) =>
            JsonSerializer.Serialize(new
            {
                @event = EventType,
                invoiceNo = payload.InvoiceNo,
                uuid = payload.Uuid,
                status = payload.Status,
                tin,
                timestampUtc = DateTime.UtcNow.ToString("O")
            }, PayloadOpts);

        private async Task RecordAttemptAsync(WebhookSubscription sub, string outcome, CancellationToken ct)
        {
            sub.LastDeliveryAtUtc = DateTime.UtcNow;
            sub.LastDeliveryResult = outcome.Length > 500 ? outcome[..500] : outcome;
            try { await _db.SaveChangesAsync(ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not record webhook delivery outcome for subscription {Id}.", sub.Id); }
        }

        /// <summary>
        /// Rejects callback URLs that are malformed, the wrong scheme, or (SSRF mitigation) resolve to a
        /// loopback/private/link-local address when <see cref="WebhookSettings.BlockPrivateNetworks"/> is on.
        /// </summary>
        private void ValidateCallbackUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"Webhook callback URL is not a valid absolute URL: '{url}'.");

            var isHttp = uri.Scheme == Uri.UriSchemeHttp;
            var isHttps = uri.Scheme == Uri.UriSchemeHttps;
            if (!isHttp && !isHttps)
                throw new InvalidOperationException($"Webhook callback URL must be http(s): '{url}'.");
            if (_settings.RequireHttps && !isHttps)
                throw new InvalidOperationException($"Webhook callback URL must use HTTPS: '{url}'.");

            if (!_settings.BlockPrivateNetworks)
                return;

            if (uri.IsLoopback)
                throw new InvalidOperationException($"Webhook callback URL resolves to a loopback host: '{url}'.");

            System.Net.IPAddress[] addresses;
            try
            {
                addresses = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
                    ? new[] { System.Net.IPAddress.Parse(uri.Host) }
                    : System.Net.Dns.GetHostAddresses(uri.DnsSafeHost);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not resolve webhook host '{uri.Host}': {ex.Message}", ex);
            }

            if (addresses.Any(IsPrivate))
                throw new InvalidOperationException(
                    $"Webhook callback URL resolves to a private/reserved address: '{url}' (set Webhooks:BlockPrivateNetworks=false to allow).");
        }

        private static bool IsPrivate(System.Net.IPAddress ip)
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return b[0] == 10                                   // 10.0.0.0/8
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                    || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 link-local
                    || b[0] == 127;                                 // 127.0.0.0/8
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

            return false;
        }
    }
}
