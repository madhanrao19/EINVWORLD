// ✅ Shared Base PageModel: Helpers/SecureFormPageModel.cs
using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace eInvWorld.Helpers
{
    public abstract class SecureFormPageModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        public SecureFormPageModel(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected bool IsHoneypotTriggered() =>
            !string.IsNullOrWhiteSpace(Request.Form["Website"]);

        protected async Task<bool> IsTurnstileValidAsync()
        {
            var token = Request.Form["cf-turnstile-response"];
            return await VerifyTurnstileAsync(_httpClientFactory.CreateClient(), _config["Turnstile:VerifyUrl"],
                _config["Turnstile:SecretKey"], token, _logger, HttpContext.RequestAborted);
        }

        /// <summary>Cloudflare's siteverify endpoint, used when Turnstile:VerifyUrl isn't configured.</summary>
        public const string DefaultTurnstileVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        /// <summary>
        /// Server-side Turnstile check. Fails closed: a missing token, a rejected token, or Cloudflare being unreachable
        /// or answering garbage all return false, so the form is refused with the normal message instead of a 500.
        /// </summary>
        internal static async Task<bool> VerifyTurnstileAsync(HttpClient client, string? verifyUrl, string? secret, string? token,
            ILogger logger, System.Threading.CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;
            try
            {
                using var response = await client.PostAsync(string.IsNullOrWhiteSpace(verifyUrl) ? DefaultTurnstileVerifyUrl : verifyUrl,
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("secret", secret ?? string.Empty),
                        new KeyValuePair<string, string>("response", token)
                    }), ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Turnstile verification returned HTTP {Status}; form refused.", (int)response.StatusCode);
                    return false;
                }
                var result = await response.Content.ReadFromJsonAsync<TurnstileResult>(cancellationToken: ct);
                return result?.success == true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
            {
                if (ct.IsCancellationRequested) throw;
                logger.LogWarning(ex, "Turnstile verification could not be completed; form refused.");
                return false;
            }
        }

        internal sealed class TurnstileResult { public bool success { get; set; } }
    }
}
