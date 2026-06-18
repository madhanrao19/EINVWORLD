// ✅ Shared Base PageModel: Helpers/SecureFormPageModel.cs
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
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("secret", _config["Turnstile:SecretKey"] ?? string.Empty),
                new KeyValuePair<string, string>("response", token!)
                }));

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<TurnstileResult>();
            return result?.success == true;
        }

        private class TurnstileResult { public bool success { get; set; } }
    }
}
