using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using eInvWorld.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using EINVWORLD.Helpers;
public class LHDNApiService : ILHDNApiService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly string? _onBehalfOf;
    private readonly ILogger<LHDNApiService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly eInvWorld.Services.IDocumentSigningService _signingService;
    private readonly EINVWORLD.Services.Audit.IAuditService _audit;

    public LHDNApiService(
        HttpClient httpClient,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<LHDNApiService> logger,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        eInvWorld.Services.IDocumentSigningService signingService,
        EINVWORLD.Services.Audit.IAuditService audit)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _context = context;
        _signingService = signingService;
        _audit = audit;

        var lhdnApiConfig = configuration.GetSection("LHDNApiConfig");
        _httpClient.BaseAddress = new Uri(lhdnApiConfig["BaseUrl"] ?? throw new InvalidOperationException("Missing configuration: LHDNApiConfig:BaseUrl"));
        _onBehalfOf = lhdnApiConfig["OnBehalfOf"];
    }

    public string? GetSystemOnBehalfOf() => _onBehalfOf;

    public async Task<DocumentSummary?> GetDocumentDetailsWithRetryAsync(string uuid, string accessToken, int maxRetries = 5)
    {
        try
        {
            // SendWithRetryAsync already handles retries, so we just call it once here.
            return await GetDocumentDetailsAsync(uuid, accessToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.Message.Contains("404"))
        {
            _logger.LogWarning("⚠️ Document not found (404) for UUID {UUID}. This may be normal for recently submitted documents.", uuid);
            return null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || ex.Message.Contains("429"))
        {
            _logger.LogWarning("⚠️ LHDN API Rate Limited (429) for UUID {UUID}. Aborting cycle.", uuid);
            throw; // Re-throw so the caller knows to break the batch loop!
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ Failed to get document details for UUID {UUID}. Error: {Msg}", uuid, ex.Message);
            return null;
        }
    }
    public async Task<string> ValidateTaxpayerAsync(string tin, string idType, string idValue)
    {
        if (string.IsNullOrWhiteSpace(tin) || string.IsNullOrWhiteSpace(idType) || string.IsNullOrWhiteSpace(idValue))
        {
            _logger.LogWarning("Validation parameters are missing or invalid.");
            throw new ArgumentException("TIN, ID Type, and ID Value are required.");
        }

        try
        {
            _logger.LogInformation("Validating taxpayer with TIN: {TIN}", LogSanitizer.MaskTin(tin));

            // ✅ Step 1: Get logged-in user's assigned TIN
            var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext!.User);
            if (user == null)
            {
                _logger.LogWarning("No logged-in user found.");
                throw new UnauthorizedAccessException("User not logged in.");
            }

            var userTin = await _context.UserCompanies
                .Where(uc => uc.UserId == user.Id)
                .Include(uc => uc.PartyInfo) // Join with PartyInfo table
                .Select(uc => uc.PartyInfo.TIN) // ✅ Get the TIN
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userTin))
            {
                _logger.LogError("No TIN found for user {userId}.", user.Id);
                throw new Exception("User's TIN is missing.");
            }

            // Log the user id, not the email (PII), and never the email at Information level.
            _logger.LogInformation("User {UserId} is assigned to company TIN: {UserTin}", user.Id, LogSanitizer.MaskTin(userTin));

            // ✅ Step 2: Determine whether user is an intermediary
            bool isIntermediary = userTin != _onBehalfOf;

            // ✅ Step 3: Get the correct access token based on TIN
            string accessToken = await _tokenService.GetAccessTokenForTIN(tin);


            // ✅ Step 4: Call the API with the correct access token

            //string accessToken = await LoginAsTaxpayerAsync();

            var requestUri = $"api/v1.0/taxpayer/validate/{tin}?idType={idType}&idValue={idValue}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            // Log only the method + URI, never the HttpRequestMessage itself (avoids leaking headers/token).
            _logger.LogDebug("Outgoing LHDN request: {Method} {Uri}", request.Method, request.RequestUri);

            // Reuse the shared retry helper: it clones the request per attempt, honours the LHDN
            // Retry-After on 429, and ensures success (throws on failure). The previous inline path
            // ("delay 5s, then re-send the SAME request") both ignored Retry-After and threw, because a
            // sent HttpRequestMessage cannot be re-sent. tin is intentionally not passed as onbehalfof —
            // taxpayer validation uses the taxpayer's own token, matching the prior behaviour.
            var response = await SendWithRetryAsync(request, accessToken);

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Taxpayer validation successful for TIN: {TIN}.", LogSanitizer.MaskTin(tin));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while validating taxpayer with TIN: {TIN}.", LogSanitizer.MaskTin(tin));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating taxpayer with TIN: {TIN}.", LogSanitizer.MaskTin(tin));
            throw;
        }
    }

    public async Task<string> SubmitDocumentsAsync(List<eInvWorld.Models.JsonModels.Documents> documents, string? tin = null)
    {
        var stopwatch = Stopwatch.StartNew();
        string requestBody = string.Empty;
        string responseContent = string.Empty;
        var statusCode = System.Net.HttpStatusCode.InternalServerError;

        // Local idempotency: hash the (pre-signing) payload so an identical resubmission within the
        // dedup window replays the prior response instead of creating a duplicate at LHDN. Computed
        // BEFORE signing because a XAdES signature embeds a timestamp and would change every time.
        string payloadHash = ComputeSubmissionPayloadHash(documents, _signingService.Enabled);

        try
        {
            var priorResponse = await TryGetRecentSubmissionResponseAsync(tin, payloadHash);
            if (priorResponse is not null)
            {
                _logger.LogWarning(
                    "♻️ Duplicate submission detected (same payload within {Minutes} min) for TIN {Tin} — " +
                    "returning the previous response instead of resubmitting.", SubmissionDedupWindowMinutes, tin is null ? "(session)" : LogSanitizer.MaskTin(tin));
                return priorResponse;
            }

            // FIX 1: Fetch the token using the explicit TIN if provided (Background Worker)
            // Otherwise, fallback to the generic token method (Manual UI Submissions)
            string accessToken;
            if (!string.IsNullOrWhiteSpace(tin))
            {
                accessToken = await _tokenService.GetAccessTokenForTIN(tin);
            }
            else
            {
                accessToken = await _tokenService.GetAccessToken(); // This throws the session error if Session is empty
            }

            var requestUri = "api/v1.0/documentsubmissions";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            // FIX 2: Add the required 'onbehalfof' header using the TIN
            if (!string.IsNullOrWhiteSpace(tin))
            {
                AddOnBehalfOfHeader(request, tin);
            }

            // v1.1 digital signing (central chokepoint for ALL submission paths). No-op when
            // SigningEnabled is false, so v1.0 unsigned behaviour is unchanged until enabled in config.
            if (_signingService.Enabled)
            {
                foreach (var d in documents)
                {
                    var rawJson = Encoding.UTF8.GetString(Convert.FromBase64String(d.Document));
                    var signedJson = _signingService.PrepareDocumentForSubmission(rawJson);
                    d.Document = Base64Encode(signedJson);     // re-encode the signed document
                    d.DocumentHash = ComputeSHA256Hash(signedJson); // hash must match the signed payload
                }
                _logger.LogInformation("🔏 Applied v1.1 signature to {Count} document(s).", documents.Count);
            }

            var documentSubmission = new
            {
                documents = documents.Select(d => new
                {
                    format = d.Format,
                    documentHash = d.DocumentHash,
                    codeNumber = d.CodeNumber,
                    document = d.Document
                }).ToArray()
            };

            requestBody = Newtonsoft.Json.JsonConvert.SerializeObject(documentSubmission);
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            statusCode = response.StatusCode;
            responseContent = await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            // Log the full transaction with masking
            LogApiTransaction("POST", requestUri, requestBody, responseContent, stopwatch.ElapsedMilliseconds, statusCode);

            // Existing logic
            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(responseContent);
                var accepted = json["acceptedDocuments"];
                if (accepted?.HasValues == true)
                {
                    _logger.LogInformation("✅ Accepted document(s): {Accepted}", accepted.ToString());
                    await RecordSubmissionAsync(tin, payloadHash, documents.Count, responseContent);
                    await _audit.WriteAsync("InvoiceSubmitted", new EINVWORLD.Services.Audit.AuditEntry
                    {
                        Tin = tin,
                        InvoiceNo = string.Join(",", documents.Select(d => d.CodeNumber)),
                        NewValueJson = accepted.ToString()
                    });
                    return responseContent;
                }

                var rejected = json["rejectedDocuments"];
                if (rejected?.HasValues == true)
                {
                    _logger.LogWarning("⚠️ Rejected document(s): {Rejected}", rejected.ToString());
                }
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException($"❌ Error submitting document: {response.StatusCode} - {responseContent}");

            throw new HttpRequestException($"❌ Submission failed. No accepted documents. Status: {response.StatusCode}. Response: {responseContent}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogApiTransaction("POST", "api/v1.0/documentsubmissions", requestBody, $"EXCEPTION: {ex.Message}", stopwatch.ElapsedMilliseconds, statusCode);
            throw;
        }
    }

    public async Task<string?> GetSubmissionStatusAsync(string submissionUid, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1.0/documentsubmissions/{submissionUid}");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        return json["overallStatus"]?.ToString();
    }

    public async Task<DocumentSummary> PollSubmissionStatusAsync(string submissionUid, string accessToken)
    {
        int maxAttempts = 10; // 🔼 Slightly increased
        int delayMs = 5000;  // Initial delay
        int backoffMultiplier = 2; // 🔁 Exponential backoff

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1.0/documentsubmissions/{submissionUid}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var parsed = JsonConvert.DeserializeObject<Submission>(responseContent);
                    var docSummary = parsed?.documentSummary?.FirstOrDefault();

                    if (docSummary != null)
                    {
                        _logger.LogInformation("✅ Polling success for SubmissionID {SubmissionUid} on attempt {Attempt}.", submissionUid, attempt);
                        return docSummary;
                    }

                    _logger.LogWarning("⚠️ [Attempt {Attempt}] Received empty document summary for SubmissionID {SubmissionUid}. Retrying...", attempt, submissionUid);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("🔄 [Attempt {Attempt}] Submission UUID {SubmissionUid} is not found. Please ensure the Submission UUID is correct. Waiting {DelayMs}ms before retry...", attempt, submissionUid, delayMs);
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Terminal failure: a 401 won't fix itself by retrying. Fail fast so the caller
                    // can refresh the token / surface the error instead of burning all the attempts.
                    _logger.LogWarning("🔒 [Attempt {Attempt}] Unauthorized (401) for SubmissionID {SubmissionUid}. Access token may be expired or invalid. Aborting polling.", attempt, submissionUid);
                    throw new UnauthorizedAccessException($"Polling failed: Unauthorized - {responseContent}");
                }
                else
                {
                    throw new Exception($"Polling failed: {response.StatusCode} - {responseContent} (SubmissionID: {submissionUid})");
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Do NOT swallow/retry terminal auth failures.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("❗ [Attempt {Attempt}] Exception occurred for SubmissionID {SubmissionUid}: {Message}", attempt, submissionUid, ex.Message);
            }

            await Task.Delay(delayMs);
            delayMs *= backoffMultiplier; // 🔁 exponential backoff
        }

        throw new Exception($"Polling failed: NotFound after retries. (SubmissionID: {submissionUid})");
    }

    // Bulk Search
    public async Task<List<DocumentSummary>> BulkSearchDocumentsAsync(string tin, DateTime start, DateTime end)
    {
        var accessToken = await _tokenService.GetAccessTokenForTIN(tin);
        var url = $"api/v1.0/documents/search?dateFrom={start:yyyy-MM-dd}&dateTo={end:yyyy-MM-dd}&status=All";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var obj = JsonConvert.DeserializeObject<JObject>(json);
        return obj?["result"]?.ToObject<List<DocumentSummary>>() ?? new List<DocumentSummary>();
    }





    public async Task<string> RejectDocumentAsync(string documentId, string rejectionReason, string tin)
    {
        if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Document ID and rejection reason are required.");

        string accessToken = await _tokenService.GetAccessTokenForTIN(tin); // ✅ Explicit token by TIN

        var requestUri = $"api/v1.0/documents/state/{documentId}/state";
        var requestBody = new { status = "rejected", reason = rejectionReason };

        var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("🚫 RejectDocument response for UUID {UUID}: {StatusCode}", documentId, response.StatusCode);
        _logger.LogDebug("📄 Response content: {Content}", responseContent);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"❌ Reject failed: {response.StatusCode} - {responseContent}");

        await _audit.WriteAsync("DocumentRejected", new EINVWORLD.Services.Audit.AuditEntry
        {
            Tin = tin,
            Uuid = documentId,
            NewValueJson = JsonConvert.SerializeObject(new { reason = rejectionReason })
        });

        return responseContent;
    }


    public async Task<string> CancelDocumentAsync(string documentId, string cancellationReason, string tin)
    {
        if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(cancellationReason))
            throw new ArgumentException("Document ID and cancellation reason are required.");

        // ✅ Explicit token by TIN (was GetAccessToken(), which relied on session state and
        // ignored the passed-in tin — breaking intermediary / on-behalf-of cancellations).
        var accessToken = await _tokenService.GetAccessTokenForTIN(tin);
        var requestUri = $"api/v1.0/documents/state/{documentId}/state";

        var requestBody = new { status = "cancelled", reason = cancellationReason };
        var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = jsonContent;

        // Add the required intermediary 'onbehalfof' header (mirrors Reject/Search).
        AddOnBehalfOfHeader(request, tin);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Cancel failed: {response.StatusCode} - {responseContent}");

        await _audit.WriteAsync("DocumentCancelled", new EINVWORLD.Services.Audit.AuditEntry
        {
            Tin = tin,
            Uuid = documentId,
            NewValueJson = JsonConvert.SerializeObject(new { reason = cancellationReason })
        });

        return responseContent;
    }

    public async Task<(List<DocumentSummary> Invoices, Metadata Metadata)> SearchDocumentsAsync(SearchDocumentInput input, string tin)
    {
        var accessToken = await _tokenService.GetAccessTokenForTIN(tin);

        var queryString = input.GetQueryString();
        var requestUri = $"api/v1.0/documents/search{queryString}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        // 🚀 FIX: Add the required intermediary header
        AddOnBehalfOfHeader(request, tin);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseJson = JsonConvert.DeserializeObject<JObject>(responseContent);

        var invoices = responseJson?["result"]?.ToObject<List<DocumentSummary>>() ?? new List<DocumentSummary>();
        var metadata = responseJson?["metadata"]?.ToObject<Metadata>();

        return (invoices, metadata!);
    }

    public async Task<DocumentSummary> GetDocumentDetailsAsync(string uuid, string accessToken, string? tin = null)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));

        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1.0/documents/{uuid}/raw");

        // ✅ USE THE HELPER: It automatically adds the Auth and OnBehalfOf headers and handles 429s
        var response = await SendWithRetryAsync(request, accessToken, tin);

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<DocumentSummary>(responseContent)!;
    }


    public string Base64Encode(string content) => Convert.ToBase64String(Encoding.UTF8.GetBytes(content));

    public string ComputeSHA256Hash(string content)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            var hex = new StringBuilder();
            foreach (var b in hashBytes) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }

    // ── Submission idempotency helpers ──────────────────────────────────────────────────────
    // Mirrors MyInvois' own 422 DuplicateSubmission detection (identical payload within ~10 min).
    private const int SubmissionDedupWindowMinutes = 10;

    /// <summary>Order-independent SHA-256 of the (pre-signing) documents in a submission batch.</summary>
    /// <param name="signingEnabled">
    /// Folded into the hash so a signed and an unsigned submission of the same payload never collide —
    /// otherwise toggling SigningEnabled could replay a cached UNSIGNED response for a now-signed submit.
    /// </param>
    private string ComputeSubmissionPayloadHash(List<eInvWorld.Models.JsonModels.Documents> documents, bool signingEnabled)
    {
        var canonical = string.Join("\n",
            documents
                .OrderBy(d => d.CodeNumber, StringComparer.Ordinal)
                .Select(d => $"{d.CodeNumber}|{d.Format}|{d.Document}"));
        canonical = $"signed={signingEnabled}\n{canonical}";
        return ComputeSHA256Hash(canonical);
    }

    /// <summary>Returns the response from a matching successful submission inside the dedup window, else null.</summary>
    private async Task<string?> TryGetRecentSubmissionResponseAsync(string? tin, string payloadHash)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-SubmissionDedupWindowMinutes);
            var record = await _context.SubmissionRecords
                .Where(s => s.PayloadHash == payloadHash
                            && s.Tin == tin
                            && s.SubmittedAtUtc >= cutoff
                            && s.ResponseContent != null)
                .OrderByDescending(s => s.SubmittedAtUtc)
                .FirstOrDefaultAsync();
            return record?.ResponseContent;
        }
        catch (Exception ex)
        {
            // Idempotency is a best-effort safety net — never let it block a real submission.
            _logger.LogWarning(ex, "Submission idempotency lookup failed; proceeding without it.");
            return null;
        }
    }

    /// <summary>Records a successful submission so an identical retry within the window is short-circuited.</summary>
    private async Task RecordSubmissionAsync(string? tin, string payloadHash, int documentCount, string responseContent)
    {
        try
        {
            _context.SubmissionRecords.Add(new eInvWorld.Models.Background.SubmissionRecord
            {
                Tin = tin,
                PayloadHash = payloadHash,
                DocumentCount = documentCount,
                SubmittedAtUtc = DateTime.UtcNow,
                ResponseContent = responseContent
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record submission idempotency row (non-fatal).");
        }
    }

    public async Task<List<string>> GetAllUuidsForTinAsync(string tin, string accessToken, int lookbackDays = 3)
    {
        var uuids = new List<string>();
        if (lookbackDays < 1) lookbackDays = 1;
        DateTime start = DateTime.Today.AddDays(-lookbackDays);
        DateTime end = DateTime.Today;
        int pageSize = 100;
        int chunkSize = 10;

        while (start <= end)
        {
            DateTime chunkEnd = start.AddDays(chunkSize - 1);
            if (chunkEnd > end) chunkEnd = end;

            int page = 1;
            bool hasMore = true;

            while (hasMore)
            {
                string url = $"api/v1.0/documents/search?issueDateFrom={start:yyyy-MM-dd}&issueDateTo={chunkEnd:yyyy-MM-dd}&page={page}&size={pageSize}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                try
                {
                    // ✅ USE THE HELPER: It handles the 429 retries and headers automatically
                    var response = await SendWithRetryAsync(request, accessToken, tin);
                    var content = await response.Content.ReadAsStringAsync();

                    var obj = JsonConvert.DeserializeObject<JObject>(content);
                    var docs = obj?["result"] as JArray;

                    if (docs == null || docs.Count == 0)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        foreach (var doc in docs)
                        {
                            var uuid = doc["uuid"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(uuid))
                                uuids.Add(uuid);
                        }

                        hasMore = docs.Count == pageSize;
                        page++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❗ Exception during UUID sync. TIN: {TIN}, Date: {Start}→{End}, Page: {Page}", LogSanitizer.MaskTin(tin), start, chunkEnd, page);
                    throw;
                }
            }

            start = chunkEnd.AddDays(1); // move to next 10-day window
        }

        _logger.LogInformation("✅ Retrieved {Count} UUIDs from LHDN for TIN: {TIN}", uuids.Count, LogSanitizer.MaskTin(tin));
        return uuids;
    }



    private HttpRequestMessage CreateLhdnRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }


    /// <summary>
    /// Redacts secrets and PII before any body is written to a log sink. Strips bearer tokens and
    /// access_token values, and masks IC numbers. Used only on the Debug path.
    /// </summary>
    private static string Redact(string? content)
    {
        if (string.IsNullOrEmpty(content)) return "N/A";
        var rx = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        // Bearer tokens / access_token values that might appear in a body.
        content = System.Text.RegularExpressions.Regex.Replace(content, @"(bearer\s+)[A-Za-z0-9\-._~+/]+=*", "$1***", rx);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"(""access_token""\s*:\s*"")[^""]+", "$1***", rx);
        // IC numbers (12-digit or 999999-99-9999).
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\d{6}-?\d{2}-?\d{4}", "******-**-****");
        return content;
    }

    private void LogApiTransaction(string method, string url, string? request, string? response, long durationMs, System.Net.HttpStatusCode statusCode)
    {
        // Metadata only at Information — never log full request/response bodies (PII + secrets) here.
        _logger.LogInformation(
            "🌐 LHDN API | {Method} {Url} | Status: {Status} | Time: {Duration}ms",
            method, url, statusCode, durationMs);

        // Full bodies only when Debug is explicitly enabled, and only after redaction.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "🌐 LHDN API body | {Method} {Url}\n➡️ Request: {Request}\n⬅️ Response: {Response}",
                method, url, Redact(request), Redact(response));
        }
    }
    private void AddOnBehalfOfHeader(HttpRequestMessage request, string tin)
    {
        if (!string.IsNullOrWhiteSpace(tin) && tin != _onBehalfOf)
        {
            request.Headers.Add("onbehalfof", tin);
        }
    }
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, string accessToken, string? tin = null)
    {
        int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            var clonedRequest = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content
            };
            clonedRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            if (!string.IsNullOrEmpty(tin))
            {
                clonedRequest.Headers.Add("onbehalfof", tin);
            }

            var response = await _httpClient.SendAsync(clonedRequest);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var content = await response.Content.ReadAsStringAsync();
                int delaySeconds = 60; // Default

                // 1. Check Standard HTTP Retry-After header first
                if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                {
                    delaySeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                }
                else
                {
                    // 2. Fallback to extracting from LHDN body
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"Try again in (\d+) seconds");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedSeconds))
                    {
                        delaySeconds = parsedSeconds;
                    }
                }

                // The server-told delay is authoritative and is never shortened, but a repeated 429 despite
                // honouring it (still-congested LHDN) grows the wait per attempt, and jitter is added so
                // multiple invoices retrying at once don't all wake up in the same instant and re-trigger
                // the limit together.
                var growthMultiplier = 1.0 + (0.5 * i); // attempt 0 -> 1x, 1 -> 1.5x, 2 -> 2x
                var jitterMs = Random.Shared.Next(250, 1500);
                var waitMs = (int)(delaySeconds * 1000 * growthMultiplier) + 2000 + jitterMs; // + 2s safety buffer

                _logger.LogWarning("⏳ 429 Too Many Requests. LHDN Penalty: Waiting {WaitMs}ms (server said {Delay}s) before retry {Attempt}/{Max}...", waitMs, delaySeconds, i + 1, maxRetries);

                await Task.Delay(waitMs);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        // 🚀 CRITICAL FIX: Must throw HttpRequestException so the upstream catch blocks recognize it as a 429!
        throw new HttpRequestException("429 ❌ LHDN API Rate Limit exceeded and all retries failed.", null, System.Net.HttpStatusCode.TooManyRequests);
    }
}




