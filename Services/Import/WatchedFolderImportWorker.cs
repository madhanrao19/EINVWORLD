using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Import
{
    /// <summary>Bound from "WatchedFolderImport". OFF by default.</summary>
    public sealed class WatchedFolderOptions
    {
        public const string SectionName = "WatchedFolderImport";

        public bool Enabled { get; set; } = false;

        /// <summary>Folder to watch, e.g. E:\EINVWORLD\Inbox. Files land here; results go to Processed/ or Rejected/.</summary>
        public string InboxPath { get; set; } = string.Empty;

        public int PollSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Hands-off ingestion: watches a folder for dropped CSV/XLSX invoice files, validates each against
    /// the real LHDN reference codes (reusing the bulk-import validator), writes a JSON report beside it,
    /// and moves it to a Processed/ or Rejected/ subfolder. Validation-only — it never creates invoices.
    /// </summary>
    public sealed class WatchedFolderImportWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WatchedFolderOptions _options;
        private readonly ILogger<WatchedFolderImportWorker> _log;

        public WatchedFolderImportWorker(IServiceScopeFactory scopeFactory, WatchedFolderOptions options,
            ILogger<WatchedFolderImportWorker> log)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _log.LogInformation("WatchedFolderImportWorker disabled (WatchedFolderImport:Enabled=false).");
                return;
            }
            if (string.IsNullOrWhiteSpace(_options.InboxPath))
            {
                _log.LogWarning("WatchedFolderImport enabled but InboxPath is empty — worker idle.");
                return;
            }

            var delay = TimeSpan.FromSeconds(_options.PollSeconds <= 0 ? 30 : _options.PollSeconds);
            _log.LogInformation("WatchedFolderImportWorker watching {Path} every {Sec}s.", _options.InboxPath, delay.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ProcessOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "WatchedFolderImportWorker cycle error"); }

                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ProcessOnceAsync(CancellationToken ct)
        {
            var inbox = _options.InboxPath;
            if (!Directory.Exists(inbox)) return; // not mounted yet — try again next cycle

            var processed = Path.Combine(inbox, "Processed");
            var rejected = Path.Combine(inbox, "Rejected");
            Directory.CreateDirectory(processed);
            Directory.CreateDirectory(rejected);

            var files = Directory.EnumerateFiles(inbox)
                .Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                byte[] bytes;
                try
                {
                    // Exclusive open so we skip files still being copied in.
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
                    using var ms = new MemoryStream();
                    await fs.CopyToAsync(ms, ct);
                    bytes = ms.ToArray();
                }
                catch (IOException)
                {
                    continue; // locked / mid-copy — next cycle
                }

                var name = Path.GetFileName(file);
                var isCsv = name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

                ImportPreview preview;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var importer = scope.ServiceProvider.GetRequiredService<IBulkInvoiceImportService>();
                    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

                    using (var ms = new MemoryStream(bytes))
                        preview = await importer.ParseAndValidateAsync(ms, isCsv, ct);

                    await audit.WriteAsync("WatchedFolderImportValidated", new AuditEntry
                    {
                        NewValueJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            file = name, rows = preview.TotalRows, errorRows = preview.ErrorRows, preview.ParseError
                        }),
                        UserNameOverride = "WatchedFolder"
                    });
                }

                var ok = string.IsNullOrEmpty(preview.ParseError) && preview.ErrorRows == 0;
                MoveWithReport(file, ok ? processed : rejected, preview);
                _log.LogInformation("WatchedFolder: {File} -> {Dest} ({Ok} ok, {Err} error rows).",
                    name, ok ? "Processed" : "Rejected", preview.OkRows, preview.ErrorRows);
            }
        }

        private void MoveWithReport(string file, string destDir, ImportPreview preview)
        {
            var name = Path.GetFileName(file);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var dest = Path.Combine(destDir, $"{stamp}_{name}");

            try
            {
                File.Move(file, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WatchedFolder: failed to move {File}", name);
                return;
            }

            try
            {
                var report = System.Text.Json.JsonSerializer.Serialize(new
                {
                    file = name,
                    processedAtUtc = DateTime.UtcNow,
                    preview.ParseError,
                    preview.TotalRows,
                    preview.OkRows,
                    preview.ErrorRows,
                    rows = preview.Rows.Select(r => new
                    {
                        r.RowNumber,
                        hasError = r.HasError,
                        issues = r.Issues.Select(i => new { severity = i.Severity.ToString(), i.Column, i.Message })
                    })
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(dest + ".report.json", report);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "WatchedFolder: failed to write report for {File}", name);
            }
        }
    }
}
