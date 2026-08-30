using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Models.SmartCapture;
using eInvWorld.Models.ViewModels;
using EINVWORLD.Services.Assistant;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>One named gating condition and whether it passed, for the audit record and for the
    /// "why wasn't this auto-submitted" trail. Never a score — every check is a deterministic pass/fail.</summary>
    public sealed record AutoSubmitCheck(string Name, bool Passed, string Detail);

    public sealed record AutoSubmitEligibilityResult(bool Eligible, IReadOnlyList<AutoSubmitCheck> Checks);

    /// <summary>
    /// Smart Capture Stage 4 (reduced first cut): decides whether an already-confirmed Smart Capture draft
    /// (created by the unchanged, human-driven SmartCaptureReviewModel.OnPostConfirmAsync flow) is eligible
    /// to be submitted to LHDN automatically instead of waiting for a manual "Submit" click on InvoiceEdit.
    ///
    /// This service NEVER submits anything itself and NEVER creates or edits an InvoiceHeader — it only
    /// decides whether to enqueue the exact same SyncJobType.SubmitDocument job the "retry a failed
    /// submission" path already uses (InvoiceSubmissionHelper.SubmitInvoiceAsync, with its existing
    /// idempotency/payload-hash guard, XAdES signing, retry/backoff, audit chain — all untouched), with a
    /// delay (SmartCaptureAutoSubmitSettings.DelayMinutes) so the confirming user has a window to cancel it
    /// on the Smart Capture list page before it actually fires.
    ///
    /// Every gating condition is deterministic (doc type allowlist, zero review warnings/errors, exact
    /// buyer match, no duplicate flag, value ceiling) — never a fuzzy "confidence score", since today's AI
    /// provider returns no per-field confidence. All conditions must pass; any single failure falls back
    /// to the existing manual-submission flow with no other side effect.
    /// </summary>
    public sealed class SmartCaptureAutoSubmitEligibilityService
    {
        private readonly ApplicationDbContext _context;
        private readonly SmartCaptureOptions _options;
        private readonly IAuditService _audit;
        private readonly ILogger<SmartCaptureAutoSubmitEligibilityService> _logger;

        public SmartCaptureAutoSubmitEligibilityService(
            ApplicationDbContext context, SmartCaptureOptions options, IAuditService audit,
            ILogger<SmartCaptureAutoSubmitEligibilityService> logger)
        {
            _context = context;
            _options = options;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>Evaluates every gating condition for one just-confirmed document/draft. Never throws for
        /// an ordinary "not eligible" outcome — only for a genuine infrastructure failure, and even then the
        /// caller (SmartCaptureReviewModel.OnPostConfirmAsync) must treat this as best-effort: the draft has
        /// already been created successfully by the time this runs.</summary>
        public async Task<AutoSubmitEligibilityResult> EvaluateAsync(
            SmartCaptureDocument document, InvoiceHeaderView model, string confirmedDocTypeCode,
            string? buyerTin, IReadOnlyList<SmartCaptureReviewItemDto> reviewItems, bool reviewHasErrors,
            CancellationToken ct)
        {
            var checks = new List<AutoSubmitCheck>();

            checks.Add(new AutoSubmitCheck("GlobalKillSwitch", _options.AutoSubmitEnabled,
                _options.AutoSubmitEnabled ? "SmartCapture:AutoSubmitEnabled is true" : "SmartCapture:AutoSubmitEnabled is false"));

            var settings = await _context.SmartCaptureAutoSubmitSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CompanyPartyInfoId == document.CompanyPartyInfoId, ct);

            checks.Add(new AutoSubmitCheck("CompanyOptedIn", settings?.Enabled ?? false,
                settings is null ? "No SmartCaptureAutoSubmitSettings row for this company" : $"Enabled={settings.Enabled}"));

            if (settings is null || !settings.Enabled)
                return new AutoSubmitEligibilityResult(false, checks); // no point evaluating the rest

            var allowedDocTypes = settings.AllowedDocTypesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var docTypeAllowed = allowedDocTypes.Contains(confirmedDocTypeCode, StringComparer.OrdinalIgnoreCase);
            checks.Add(new AutoSubmitCheck("DocTypeAllowed", docTypeAllowed,
                $"Confirmed \"{confirmedDocTypeCode}\" against allowlist \"{settings.AllowedDocTypesCsv}\""));

            var zeroIssues = !reviewHasErrors && !reviewItems.Any(i => i.Severity != "Ok");
            checks.Add(new AutoSubmitCheck("ZeroReviewIssues", zeroIssues,
                $"{reviewItems.Count(i => i.Severity == "Error")} error(s), {reviewItems.Count(i => i.Severity == "Warning")} warning(s)"));

            var buyerMatched = !string.IsNullOrWhiteSpace(buyerTin);
            checks.Add(new AutoSubmitCheck("BuyerExactMatch", buyerMatched,
                buyerMatched ? "Confirmed buyer has a TIN" : "No confirmed buyer TIN"));

            var value = model.TotalPayableAmount ?? decimal.MaxValue;
            var underCeiling = value <= settings.MaxAutoSubmitValue;
            checks.Add(new AutoSubmitCheck("UnderValueCeiling", underCeiling,
                $"TotalPayableAmount {value:0.00} vs ceiling {settings.MaxAutoSubmitValue:0.00}"));

            var eligible = checks.All(c => c.Passed);
            return new AutoSubmitEligibilityResult(eligible, checks);
        }

        /// <summary>Schedules the delayed SubmitDocument job and links it to the document for the
        /// cancel-window UI. Writes the full check record to the audit trail either way (scheduled or
        /// skipped) so "why did/didn't this auto-submit" is always answerable after the fact — but only
        /// when the company has actually opted in, so a company that never enabled this feature doesn't
        /// accumulate no-op audit noise on every Smart Capture confirmation.</summary>
        public async Task ApplyAsync(
            SmartCaptureDocument document, string invoiceNo, string? supplierTin,
            AutoSubmitEligibilityResult result, CancellationToken ct)
        {
            var companyOptedIn = result.Checks.FirstOrDefault(c => c.Name == "CompanyOptedIn")?.Passed ?? false;
            if (!companyOptedIn) return; // never opted in — nothing to record

            if (!result.Eligible)
            {
                await _audit.WriteAsync("SmartCaptureAutoSubmitSkipped", new AuditEntry
                {
                    InvoiceNo = invoiceNo,
                    NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, invoiceNo, checks = result.Checks })
                }, ct);
                return;
            }

            var settings = await _context.SmartCaptureAutoSubmitSettings
                .AsNoTracking()
                .FirstAsync(s => s.CompanyPartyInfoId == document.CompanyPartyInfoId, ct);

            var job = new SyncJob
            {
                Tin = supplierTin ?? string.Empty,
                JobType = SyncJobType.SubmitDocument,
                Status = SyncJobStatus.Queued,
                QueuedAtUtc = DateTime.UtcNow,
                NextRunAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, settings.DelayMinutes)),
                MaxAttempts = 3,
                TriggeredBy = "System (Smart Capture Auto-Submit)",
                PayloadJson = SyncJobPayload.CreateForInvoice(invoiceNo),
            };
            _context.SyncJobs.Add(job);
            await _context.SaveChangesAsync(ct);

            document.PendingAutoSubmitJobId = job.Id;
            await _context.SaveChangesAsync(ct);

            await _audit.WriteAsync("SmartCaptureAutoSubmitScheduled", new AuditEntry
            {
                InvoiceNo = invoiceNo,
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, invoiceNo, jobId = job.Id, runAtUtc = job.NextRunAtUtc, checks = result.Checks })
            }, ct);

            _logger.LogInformation("Smart Capture: auto-submit scheduled for invoice {InvoiceNo} (document {DocumentId}) at {RunAtUtc}", invoiceNo, document.Id, job.NextRunAtUtc);
        }

        /// <summary>Cancels a still-pending auto-submission. Uses a single atomic conditional UPDATE
        /// (<c>WHERE Id = jobId AND Status = Queued</c>) rather than a load-check-save round trip: SyncJob
        /// has no concurrency token, so a plain read-then-write here would race the durable worker's own
        /// atomic claim (<see cref="DurableSyncJobWorker"/> claims via UPDLOCK/READPAST inside a short
        /// transaction, then releases the lock and can run — and complete, including a real LHDN
        /// submission — well before this method's own read would ever notice). A stale read winning that
        /// race and blindly overwriting the row afterwards would show "Cancelled" even though the invoice
        /// had already been submitted — this atomic UPDATE closes that window: it can only ever affect a
        /// row still Queued at the exact moment it runs, and the affected-row-count tells us which case we
        /// hit. Caller must have already verified the document belongs to the acting user's company.</summary>
        public async Task<bool> CancelAsync(SmartCaptureDocument document, CancellationToken ct)
        {
            if (document.PendingAutoSubmitJobId is not int jobId) return false;

            var cancelled = await _context.SyncJobs
                .Where(j => j.Id == jobId && j.Status == SyncJobStatus.Queued)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, SyncJobStatus.Cancelled), ct);

            // Either way the job is no longer in a cancellable state — clear the pending marker so a stale
            // "Auto-submit pending" badge doesn't linger once it's already run (or just got cancelled).
            document.PendingAutoSubmitJobId = null;
            await _context.SaveChangesAsync(ct);

            if (cancelled == 0) return false; // lost the race — the worker had already claimed/run it

            await _audit.WriteAsync("SmartCaptureAutoSubmitCancelled", new AuditEntry
            {
                InvoiceNo = document.RelatedInvoiceHeaderInvoiceNo,
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, jobId })
            }, ct);

            return true;
        }

        /// <summary>Retracts every still-pending auto-submission for a company in one pass — called when a
        /// system Admin disables the company's opt-in (<c>Pages/Admin/SmartCaptureAutoSubmit</c>), so
        /// turning the toggle off actually stops jobs already scheduled during their delay window, not just
        /// new ones. Same atomic conditional UPDATE as <see cref="CancelAsync"/> (a job the worker already
        /// claimed is simply left alone — it is no longer Queued, so the WHERE clause excludes it), applied
        /// in bulk instead of one row at a time. Returns how many were actually cancelled.</summary>
        public async Task<int> CancelAllPendingForCompanyAsync(int companyPartyInfoId, CancellationToken ct)
        {
            var jobIds = await _context.SmartCaptureDocuments
                .Where(d => d.CompanyPartyInfoId == companyPartyInfoId && d.PendingAutoSubmitJobId != null)
                .Select(d => d.PendingAutoSubmitJobId!.Value)
                .ToListAsync(ct);
            if (jobIds.Count == 0) return 0;

            var cancelledCount = await _context.SyncJobs
                .Where(j => jobIds.Contains(j.Id) && j.Status == SyncJobStatus.Queued)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, SyncJobStatus.Cancelled), ct);

            await _context.SmartCaptureDocuments
                .Where(d => d.CompanyPartyInfoId == companyPartyInfoId && d.PendingAutoSubmitJobId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.PendingAutoSubmitJobId, (int?)null), ct);

            if (cancelledCount > 0)
            {
                await _audit.WriteAsync("SmartCaptureAutoSubmitBulkCancelled", new AuditEntry
                {
                    NewValueJson = JsonSerializer.Serialize(new { companyPartyInfoId, cancelledCount, totalPending = jobIds.Count })
                }, ct);
            }

            return cancelledCount;
        }
    }
}
