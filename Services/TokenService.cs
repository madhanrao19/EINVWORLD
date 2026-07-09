using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Auth;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TokenService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;

        // Serializes token acquisition so concurrent callers don't all hit /connect/token at once.
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        // In-memory token cache. IMemoryCache auto-evicts entries at their absolute expiry, so it
        // cannot leak or de-sync the way the previous pair of static Dictionaries could.
        private static string CacheKey(string tin) => $"lhdn_token:{tin}";

        public TokenService(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<TokenService> logger,
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache cache)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }

        private void CacheToken(string tin, string token, DateTime expiryUtc)
        {
            if (expiryUtc <= DateTime.UtcNow || string.IsNullOrEmpty(token)) return;
            _cache.Set(CacheKey(tin), token, new MemoryCacheEntryOptions { AbsoluteExpiration = expiryUtc });
        }

        public async Task<string> GetAccessToken()
        {
            var tin = GetCurrentUserTIN();
            return await GetOrFetchTokenAsync(tin);
        }

        /// <summary>
        /// Returns a valid access token for the TIN, using (in order): the in-memory cache,
        /// the durable LHDNTokens DB row, or a freshly requested token. Token acquisition is
        /// serialized via <see cref="_tokenLock"/> with a double-check so concurrent callers for
        /// the same TIN don't each hit /connect/token.
        /// </summary>
        private async Task<string> GetOrFetchTokenAsync(string tin)
        {
            // Fast path: in-memory cache (auto-evicts at expiry).
            if (_cache.TryGetValue(CacheKey(tin), out string? cached) && !string.IsNullOrEmpty(cached))
            {
                _logger.LogDebug("✅ Using cached token for TIN {TIN}", LogSanitizer.MaskTin(tin));
                return cached!;
            }

            await _tokenLock.WaitAsync();
            try
            {
                // Re-check after acquiring the lock — another caller may have populated it.
                if (_cache.TryGetValue(CacheKey(tin), out string? cached2) && !string.IsNullOrEmpty(cached2))
                    return cached2!;

                // Durable store.
                var dbToken = await _context.LHDNTokens.FirstOrDefaultAsync(t => t.TIN == tin);
                if (dbToken != null && dbToken.ExpiryTime > DateTime.UtcNow)
                {
                    _logger.LogInformation("♻️ Using DB token for TIN {TIN}", LogSanitizer.MaskTin(tin));
                    CacheToken(tin, dbToken.AccessToken, dbToken.ExpiryTime);
                    return dbToken.AccessToken;
                }

                // Otherwise, request a new token.
                return await RequestNewTokenWithRetry(tin);
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private string GetCurrentUserTIN()
        {
            var tin = _httpContextAccessor.HttpContext?.Session.GetString("UserTIN");
            if (string.IsNullOrWhiteSpace(tin))
                throw new Exception("❌ TIN not found in session. Ensure it's stored at login.");
            return tin;
        }

        private async Task<string> RequestNewTokenWithRetry(string tin)
        {
            int retries = 0;
            while (retries < 5)
            {
                try
                {
                    return await RequestNewToken(tin);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("429"))
                {
                    retries++;
                    int delay = 2000 + retries * 1000 + Random.Shared.Next(500, 1500);
                    if (ex.Data.Contains("RetryAfterSeconds"))
                    {
                        delay = (Convert.ToInt32(ex.Data["RetryAfterSeconds"]) * 1000) + Random.Shared.Next(500, 1500);
                        _logger.LogWarning("⏳ 429 received. Retrying in {Delay}ms (TIN: {TIN})", delay, LogSanitizer.MaskTin(tin));
                    }
                    await Task.Delay(delay);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("rejected intermediary") || ex.Message.Contains("unauthorized_client"))
                {
                    _logger.LogWarning("⛔ LHDN permanently rejected TIN {TIN}. Flagging in database and stopping retries.", LogSanitizer.MaskTin(tin));

                    // ✅ Update DB and break loop
                    var partyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.TIN == tin);
                    if (partyInfo != null)
                    {
                        partyInfo.LhdnIntermediaryRejected = true;
                        await _context.SaveChangesAsync();
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Token request failed for TIN {TIN}. Retry {Attempt}/5", LogSanitizer.MaskTin(tin), retries + 1);
                    await Task.Delay(2000);
                    retries++;
                }
            }

            throw new Exception("❌ Failed to acquire token after multiple retries.");
        }

        private async Task<string> RequestNewToken(string tin)
        {
            var baseUrl = _configuration["LHDNApiConfig:BaseUrl"]
                ?? throw new InvalidOperationException("Missing configuration: LHDNApiConfig:BaseUrl");
            var tokenEndpoint = _configuration["LHDNApiConfig:TokenEndpoint"]
                ?? throw new InvalidOperationException("Missing configuration: LHDNApiConfig:TokenEndpoint");
            var clientId = _configuration["LHDNApiConfig:ClientId"]
                ?? throw new InvalidOperationException("Missing configuration: LHDNApiConfig:ClientId");
            var clientSecret1 = _configuration["LHDNApiConfig:ClientSecret"];
            var clientSecret2 = _configuration["LHDNApiConfig:ClientSecret2"];
            var scope = _configuration["LHDNApiConfig:Scope"]
                ?? throw new InvalidOperationException("Missing configuration: LHDNApiConfig:Scope");
            var systemTIN = _configuration["LHDNApiConfig:OnBehalfOf"];

            var tokenUrl = new Uri(new Uri(baseUrl), tokenEndpoint);
            var secretsToTry = new[] { clientSecret1, clientSecret2 };

            foreach (var secret in secretsToTry)
            {
                var requestContent = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId),
            new("client_secret", secret ?? string.Empty),
            new("grant_type", "client_credentials"),
            new("scope", scope)
        };

                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
                {
                    Content = new FormUrlEncodedContent(requestContent)
                };

                // PUTTING THIS BACK: It is strictly required by LHDN for Intermediaries
                bool isIntermediary = tin != systemTIN;
                if (isIntermediary)
                {
                    tokenRequest.Headers.Add("onbehalfof", tin);
                    _logger.LogInformation("🧾 Acting as Intermediary. Adding onbehalfof header to Token Request: {TIN}", LogSanitizer.MaskTin(tin));
                }
                else
                {
                    _logger.LogInformation("🧾 Acting as Taxpayer. No onbehalfof header.");
                }

                var response = await _httpClient.SendAsync(tokenRequest);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // ... (Keep your existing success logic here to save token to DB)
                    var tokenObj = JsonSerializer.Deserialize<TokenResponse>(body)
                        ?? throw new InvalidOperationException("Failed to deserialize token response.");
                    var expiry = DateTime.UtcNow.AddSeconds(tokenObj.expires_in - 120);

                    CacheToken(tin, tokenObj.access_token, expiry);

                    _logger.LogInformation("✅ Token acquired and cached for TIN {TIN}", LogSanitizer.MaskTin(tin));
                    return tokenObj.access_token;
                }

                // NEW ERROR HANDLING: Log the EXACT body message from LHDN
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // This will reveal the true error! Usually "invalid_grant" or "Intermediary not appointed"
                    _logger.LogError("❌ LHDN 400 BadRequest. They rejected the Intermediary Login. LHDN Message: {Body}", body);
                    throw new InvalidOperationException($"LHDN rejected intermediary token for {tin}. Reason: {body}");
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var ex = new InvalidOperationException("429 Too Many Requests");
                    if (response.Headers?.RetryAfter?.Delta != null)
                        ex.Data["RetryAfterSeconds"] = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                    throw ex;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("⚠️ Unauthorized with one secret. Trying fallback...");
                    continue;
                }

                throw new InvalidOperationException($"Token request failed: {response.StatusCode} - {body}");
            }

            throw new InvalidOperationException("❌ All secrets failed. Cannot get token.");
        }

        public async Task<string> GetUserAssignedTINAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                throw new Exception("❌ User not authenticated.");

            var tin = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.PartyInfo)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(tin) || tin.StartsWith("EI00"))
                throw new Exception($"❌ Invalid or General TIN ({tin}) assigned to this user.");

            return tin;
        }


        public async Task<string> GetAccessTokenForTIN(string tin)
        {
            if (GeneralTINHelper.IsGeneralTIN(tin))
                throw new Exception($"❌ Token request blocked: General TIN ({tin}) is not allowed.");

            return await GetOrFetchTokenAsync(tin);
        }

    }
}
