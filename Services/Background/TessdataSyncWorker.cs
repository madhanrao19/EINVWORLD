using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Background
{
    /// <summary>
    /// Keeps the Tesseract OCR language files (`&lt;lang&gt;.traineddata`) that
    /// <c>TesseractDocumentOcrService</c> needs present and current, replacing the previous
    /// fully-manual process (operator downloads each file from GitHub and copies it into
    /// `DocumentCapture:TessdataPath` by hand — still documented in
    /// IIS-DEPLOYMENT-GUIDE.md PART 17a-OCR as the fallback for an air-gapped server).
    ///
    /// Source: the official, FOSS (Apache-2.0) `tesseract-ocr/tessdata` GitHub repository —
    /// https://github.com/tesseract-ocr/tessdata — fetched via raw.githubusercontent.com. Only the
    /// language(s) actually configured in `DocumentCapture:OcrLanguage` (e.g. "eng" or "eng+msa") are
    /// downloaded — never every file in the repo.
    ///
    /// Sync policy (deliberately conservative — this is a large binary file, not a small JSON table):
    ///  - a HEAD request compares the upstream Content-Length against the local file's size; a file is
    ///    only (re-)downloaded when it's missing locally or the sizes differ (i.e. upstream published a
    ///    new version) — an unchanged file is never re-fetched, so a normal app-pool recycle costs one
    ///    cheap HEAD request per language, not a repeat multi-MB download;
    ///  - downloads write to a `.downloading` temp file first, then atomically replace the target via
    ///    `File.Move(..., overwrite: true)` — a request that's cancelled mid-download (app stopping,
    ///    network drop) can never leave a truncated/corrupt `.traineddata` file for the OCR engine to
    ///    load;
    ///  - one language's failure (network, 404, disk) is logged and skipped — it never blocks the
    ///    others or crashes the worker.
    ///
    /// Entirely inert unless BOTH `DocumentCapture:OcrEnabled=true` (no point staging language files
    /// for a disabled feature) AND `TessdataSync:Enabled` (default true, but independently switchable
    /// off for a server with restricted outbound internet access — OCR still works there with manually
    /// staged files, exactly as documented today). Config (TessdataSync section): Enabled, BaseUrl
    /// (default `https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/`), IntervalHours
    /// (default 24, min 1), StartupDelayMinutes (default 2).
    /// </summary>
    public class TessdataSyncWorker : BackgroundService
    {
        public const string HttpClientName = "TesseractTessdataSync";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<TessdataSyncWorker> _logger;

        public TessdataSyncWorker(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TessdataSyncWorker> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("DocumentCapture:OcrEnabled", false))
            {
                _logger.LogInformation("TessdataSyncWorker: OCR is disabled (DocumentCapture:OcrEnabled=false) — nothing to sync.");
                return;
            }

            if (!_config.GetValue("TessdataSync:Enabled", true))
            {
                _logger.LogInformation("TessdataSyncWorker disabled (TessdataSync:Enabled=false) — stage tessdata files manually (see IIS-DEPLOYMENT-GUIDE.md PART 17a-OCR).");
                return;
            }

            var tessdataPath = _config["DocumentCapture:TessdataPath"];
            if (string.IsNullOrWhiteSpace(tessdataPath))
            {
                _logger.LogWarning("TessdataSyncWorker: DocumentCapture:OcrEnabled is true but DocumentCapture:TessdataPath is empty — worker idle.");
                return;
            }

            var startupDelay = TimeSpan.FromMinutes(Math.Max(0, _config.GetValue("TessdataSync:StartupDelayMinutes", 2)));
            var interval = TimeSpan.FromHours(Math.Max(1, _config.GetValue("TessdataSync:IntervalHours", 24)));

            _logger.LogInformation("🟢 TessdataSyncWorker started. First run in {Delay} min, then every {Hours} h.",
                startupDelay.TotalMinutes, interval.TotalHours);

            try { await Task.Delay(startupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncAsync(tessdataPath, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Tessdata sync cycle failed; will retry next cycle.");
                }

                try { await Task.Delay(interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("🛑 TessdataSyncWorker stopped.");
        }

        /// <summary>Downloads any missing/outdated language file for every language configured in
        /// DocumentCapture:OcrLanguage (e.g. "eng+msa" → eng, msa). Public for the same reason
        /// CodeTableSyncWorker.SyncAllTablesAsync is — a test/manual trigger can call it directly.</summary>
        public async Task SyncAsync(string tessdataPath, CancellationToken ct)
        {
            var baseUrl = _config.GetValue("TessdataSync:BaseUrl", "https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/")!;
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            var languages = (_config["DocumentCapture:OcrLanguage"] ?? "eng")
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (languages.Count == 0) languages.Add("eng");

            Directory.CreateDirectory(tessdataPath); // first-run convenience — matches the Modify rights already granted per the deployment guide

            var http = _httpClientFactory.CreateClient(HttpClientName);
            foreach (var lang in languages)
            {
                await SyncOneAsync(http, baseUrl, tessdataPath, lang, ct);
            }
        }

        private async Task SyncOneAsync(HttpClient http, string baseUrl, string tessdataPath, string lang, CancellationToken ct)
        {
            var fileName = $"{lang}.traineddata";
            var targetPath = Path.Combine(tessdataPath, fileName);
            var url = baseUrl + fileName;

            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await http.SendAsync(headRequest, ct);
                if (!headResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("⚠️ Tessdata sync: HEAD {Url} returned {Status} — skipped.", url, (int)headResponse.StatusCode);
                    return;
                }

                var remoteLength = headResponse.Content.Headers.ContentLength;
                var localExists = File.Exists(targetPath);
                var localLength = localExists ? new FileInfo(targetPath).Length : (long?)null;

                if (localExists && remoteLength.HasValue && remoteLength.Value == localLength)
                {
                    _logger.LogInformation("✅ Tessdata sync: {File} already up to date ({Bytes} bytes) — skipped.", fileName, localLength);
                    return;
                }

                _logger.LogInformation("⬇️ Tessdata sync: downloading {File} ({Reason})...", fileName,
                    !localExists ? "missing locally" : "upstream size differs — newer version");

                var tempPath = targetPath + ".downloading";
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                    await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await contentStream.CopyToAsync(fileStream, ct);
                }

                // Sanity check: a real trained-data file is at least several hundred KB; anything smaller
                // is almost certainly an HTML error page or a truncated download, not valid tessdata —
                // never let that reach the OCR engine.
                var downloadedLength = new FileInfo(tempPath).Length;
                if (downloadedLength < 100_000)
                {
                    File.Delete(tempPath);
                    _logger.LogWarning("⚠️ Tessdata sync: downloaded {File} was implausibly small ({Bytes} bytes) — discarded, not applied.", fileName, downloadedLength);
                    return;
                }

                File.Move(tempPath, targetPath, overwrite: true);
                _logger.LogInformation("✅ Tessdata sync: {File} updated ({Bytes} bytes).", fileName, downloadedLength);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "⚠️ Tessdata sync failed for {File}; continuing with the next language (if any). OCR will keep using whatever is already on disk.", fileName);
            }
        }
    }
}
