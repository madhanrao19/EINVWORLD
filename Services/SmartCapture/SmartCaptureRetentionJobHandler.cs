using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Background;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>
    /// Durable-queue handler for SyncJobType.SmartCaptureRetention. A "sweep" job — it has no per-document
    /// payload; it evaluates every SmartCaptureDocument row whose file hasn't been cleaned up yet, against
    /// tiered retention windows (SmartCaptureOptions), and deletes the physical file (not the row — kept
    /// for audit/history) once its tier's window has elapsed. Never a single "delete everything older than
    /// X days" rule: a document linked to a submitted, LHDN-acknowledged invoice is retained far longer
    /// than a document that never got past a failed extraction.
    /// </summary>
    public sealed class SmartCaptureRetentionJobHandler : ISyncJobHandler
    {
        private const int BatchSize = 500;

        private readonly ApplicationDbContext _context;
        private readonly FilePathConfig _filePathConfig;
        private readonly SmartCaptureOptions _options;
        private readonly IAuditService _audit;
        private readonly ILogger<SmartCaptureRetentionJobHandler> _logger;

        public SmartCaptureRetentionJobHandler(
            ApplicationDbContext context,
            IOptions<FilePathConfig> filePathConfig,
            SmartCaptureOptions options,
            IAuditService audit,
            ILogger<SmartCaptureRetentionJobHandler> logger)
        {
            _context = context;
            _filePathConfig = filePathConfig.Value;
            _options = options;
            _audit = audit;
            _logger = logger;
        }

        public string JobType => SyncJobType.SmartCaptureRetention;

        public async Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.SmartCaptureDocuments
                .Where(d => d.FileDeletedAtUtc == null)
                .OrderBy(d => d.CreatedAtUtc)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return "No Smart Capture documents pending retention review.";

            var deleted = 0;
            var errors = 0;

            foreach (var document in candidates)
            {
                ct.ThrowIfCancellationRequested();

                var windowDays = await ResolveRetentionWindowDaysAsync(document, ct);
                var age = now - document.CreatedAtUtc;
                if (age.TotalDays < windowDays) continue;

                if (SafePath.TryResolve(_filePathConfig.SmartCaptureFolder, out var fullPath,
                        document.CompanyPartyInfoId.ToString(), Path.GetFileName(document.InternalStorageReference)))
                {
                    try
                    {
                        if (File.Exists(fullPath)) File.Delete(fullPath);
                        document.FileDeletedAtUtc = now;
                        document.UpdatedAtUtc = now;
                        deleted++;
                    }
                    catch (IOException ex)
                    {
                        errors++;
                        _logger.LogWarning(ex, "Retention: failed to delete Smart Capture file for document {DocumentId}", document.Id);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        errors++;
                        _logger.LogWarning(ex, "Retention: access denied deleting Smart Capture file for document {DocumentId}", document.Id);
                    }
                }
                else
                {
                    // Storage reference no longer resolvable — treat as already gone so the row isn't
                    // re-evaluated forever.
                    document.FileDeletedAtUtc = now;
                    document.UpdatedAtUtc = now;
                }
            }

            if (deleted > 0)
            {
                await _context.SaveChangesAsync(ct);
                await _audit.WriteAsync("SmartCaptureRetentionSweep", new AuditEntry
                {
                    UserNameOverride = "System (SmartCaptureRetention job)",
                    NewValueJson = JsonSerializer.Serialize(new { evaluated = candidates.Count, deleted, errors })
                }, ct);
            }

            return $"Evaluated {candidates.Count}, deleted {deleted} file(s), {errors} error(s).";
        }

        private async Task<int> ResolveRetentionWindowDaysAsync(SmartCaptureDocument document, CancellationToken ct)
        {
            if (document.Status == SmartCaptureDocumentStatus.Failed)
                return _options.RetentionDaysFailed;

            if (string.IsNullOrEmpty(document.RelatedInvoiceHeaderInvoiceNo))
            {
                // Reached ReviewRequired/ValidationFailed but the user never confirmed a draft (or is
                // still stuck in Uploaded/Queued/Processing — treat the same as "abandoned").
                return _options.RetentionDaysAbandonedReview;
            }

            // Linked to a real invoice — retention depends on whether it's still a local Draft or has
            // actually been submitted to LHDN (UUID is only ever set post-submission).
            var uuid = await _context.InvoiceHeaders
                .Where(h => h.InvoiceNo == document.RelatedInvoiceHeaderInvoiceNo)
                .Select(h => h.UUID)
                .FirstOrDefaultAsync(ct);

            return string.IsNullOrEmpty(uuid) ? _options.RetentionDaysDraftLinked : _options.RetentionDaysSubmittedLinked;
        }
    }
}
