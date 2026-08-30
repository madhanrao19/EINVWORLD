using System;
using System.Collections.Generic;
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
using EINVWORLD.Services.Assistant;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using EINVWORLD.Services.DocumentCapture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EINVWORLD.Services.SmartCapture
{
    /// <summary>
    /// Durable-queue handler for SyncJobType.SmartCaptureExtraction: loads the persisted upload, runs the
    /// same extract → OCR-fallback → LLM-suggest → LHDN-aware-review pipeline CreateFromFileModel already
    /// runs synchronously, and writes the result back onto the SmartCaptureDocument row. Never creates or
    /// touches an InvoiceHeader — that only happens on the interactive confirm postback, which has the
    /// ISession InvoiceDraftService.SaveDraft requires (unavailable here, in a BackgroundService).
    /// </summary>
    public sealed class SmartCaptureExtractionJobHandler : ISyncJobHandler
    {
        private readonly ApplicationDbContext _context;
        private readonly FilePathConfig _filePathConfig;
        private readonly SmartCaptureOptions _options;
        private readonly IDocumentTextExtractor _textExtractor;
        private readonly IDocumentOcrService _ocr;
        private readonly IEInvoiceAssistantService _assistant;
        private readonly SmartCaptureCompanyHintService _hints;
        private readonly IAuditService _audit;
        private readonly ILogger<SmartCaptureExtractionJobHandler> _logger;

        private const string SystemActor = "System (SmartCaptureExtraction job)";

        public SmartCaptureExtractionJobHandler(
            ApplicationDbContext context,
            IOptions<FilePathConfig> filePathConfig,
            SmartCaptureOptions options,
            IDocumentTextExtractor textExtractor,
            IDocumentOcrService ocr,
            IEInvoiceAssistantService assistant,
            SmartCaptureCompanyHintService hints,
            IAuditService audit,
            ILogger<SmartCaptureExtractionJobHandler> logger)
        {
            _context = context;
            _filePathConfig = filePathConfig.Value;
            _options = options;
            _textExtractor = textExtractor;
            _ocr = ocr;
            _assistant = assistant;
            _hints = hints;
            _audit = audit;
            _logger = logger;
        }

        public string JobType => SyncJobType.SmartCaptureExtraction;

        public async Task<string> ExecuteAsync(SyncJob job, CancellationToken ct)
        {
            var documentId = SyncJobPayload.SmartCaptureDocumentIdOrNull(job.PayloadJson);
            if (documentId is null)
                throw new InvalidOperationException("SmartCaptureExtraction job payload is missing SmartCaptureDocumentId.");

            var document = await _context.SmartCaptureDocuments.FirstOrDefaultAsync(d => d.Id == documentId.Value, ct);
            if (document is null)
                return $"SmartCaptureDocument {documentId} no longer exists — skipping.";

            // Idempotency: a retried job (worker crash mid-run, etc.) must not reprocess a document that
            // already reached a terminal-for-this-stage status.
            if (document.Status is SmartCaptureDocumentStatus.ReviewRequired
                or SmartCaptureDocumentStatus.ValidationFailed
                or SmartCaptureDocumentStatus.DraftCreated)
            {
                return $"Document {document.Id} already processed (status {document.Status}) — skipping duplicate run.";
            }

            document.Status = SmartCaptureDocumentStatus.Processing;
            document.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            if (!SafePath.TryResolve(_filePathConfig.SmartCaptureFolder, out var fullPath,
                    document.CompanyPartyInfoId.ToString(), Path.GetFileName(document.InternalStorageReference)))
            {
                return await FailAsync(document, "StorageUnresolvable", "Could not locate the stored file.", ct);
            }
            if (!File.Exists(fullPath))
            {
                return await FailAsync(document, "FileMissing", "The uploaded file could not be found on disk.", ct);
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            var extension = Path.GetExtension(document.OriginalFileName).TrimStart('.').ToLowerInvariant();

            string text;
            var usedOcr = false;

            if (extension == "pdf")
            {
                document.PageCount = _textExtractor.TryGetPdfPageCount(bytes);
                text = _textExtractor.ExtractPdfText(bytes, _options.MaxPages);
                if (string.IsNullOrWhiteSpace(text) && _ocr.IsAvailable)
                {
                    text = _ocr.OcrPdf(bytes, _options.MaxPages);
                    usedOcr = !string.IsNullOrWhiteSpace(text);
                }
            }
            else
            {
                // Image upload (jpg/jpeg/png) — no PDF text layer concept; OCR is the only path.
                document.PageCount = 1;
                if (!_ocr.IsAvailable)
                    return await FailAsync(document, "OcrRequiredForImage", "OCR is not enabled on this server; image uploads require OCR to be configured.", ct);

                text = _ocr.OcrImage(bytes);
                usedOcr = !string.IsNullOrWhiteSpace(text);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return await FailAsync(document, "NoTextExtracted",
                    "Couldn't read this document. If it's a scan, make sure it's clear and upright, or try a digital (text-based) PDF.", ct);
            }

            var knownBuyers = await LoadKnownBuyersForCompanyAsync(document.CompanyPartyInfoId, ct);
            var companyHints = await _hints.GetAsync(document.CompanyPartyInfoId, ct);

            var result = await _assistant.SuggestInvoiceAsync(text, knownBuyers, companyHints, ct);
            if (!result.Ok)
            {
                return await FailAsync(document, "AssistantUnavailable", result.Error ?? "The AI assistant could not process this document.", ct);
            }

            var review = _assistant.ReviewSuggestion(result.Content, knownBuyers.Select(b => b.Tin).ToList());

            // Flag (never block) an exact-content re-upload within the same company — a Warning, not an
            // Error, so it lands in the "needs a look" tier rather than SmartCaptureDocumentStatus
            // .ValidationFailed. Business-level duplicate detection (same buyer/amount/date, different
            // file) is a larger, riskier heuristic deferred to Stage 2 alongside supplier templates.
            if (await IsDuplicateUploadAsync(document, ct))
            {
                review.Warn("Possible duplicate — this file's content matches a document already captured for this company. Check it isn't a re-upload of the same invoice.");
            }

            document.NormalizedExtractionJson = JsonSerializer.Serialize(new SmartCaptureExtractionPayload
            {
                SuggestionJson = result.Content,
                ReviewItems = review.Items.Select(i => new SmartCaptureReviewItemDto(i.Severity.ToString(), i.Message)).ToList(),
                ReviewHasErrors = review.HasErrors,
                ReviewReadyForForm = review.ReadyForForm,
            });
            document.UsedOcr = usedOcr;
            document.Status = review.HasErrors ? SmartCaptureDocumentStatus.ValidationFailed : SmartCaptureDocumentStatus.ReviewRequired;
            document.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            await _audit.WriteAsync("SmartCaptureExtracted", new AuditEntry
            {
                UserNameOverride = SystemActor,
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, ocr = usedOcr, hasErrors = review.HasErrors })
            }, ct);

            return $"Document {document.Id} extracted — status {document.Status}.";
        }

        private async Task<string> FailAsync(SmartCaptureDocument document, string failureCode, string userSafeMessage, CancellationToken ct)
        {
            document.Status = SmartCaptureDocumentStatus.Failed;
            document.FailureCode = failureCode;
            document.UserSafeFailureMessage = userSafeMessage;
            document.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            await _audit.WriteAsync("SmartCaptureExtractionFailed", new AuditEntry
            {
                UserNameOverride = SystemActor,
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id, failureCode })
            }, ct);

            _logger.LogWarning("Smart Capture extraction failed for document {DocumentId}: {FailureCode}", document.Id, failureCode);
            return $"Document {document.Id} failed: {failureCode}";
        }

        /// <summary>Exact-content duplicate check, scoped to the uploading company (tenant-isolated the
        /// same way every other query on this table is) — a different company uploading byte-identical
        /// content (e.g. a shared template) is not flagged.</summary>
        private async Task<bool> IsDuplicateUploadAsync(SmartCaptureDocument document, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(document.FileHash)) return false;
            return await _context.SmartCaptureDocuments.AnyAsync(d =>
                d.Id != document.Id &&
                d.CompanyPartyInfoId == document.CompanyPartyInfoId &&
                d.FileHash == document.FileHash, ct);
        }

        /// <summary>Mirrors CreateFromFileModel.LoadKnownBuyersAsync but scoped directly by company id — a
        /// background job has no HttpContext/session to derive the acting user's membership from, and
        /// doesn't need to: the company id was already tenant-checked once, at upload time.</summary>
        private async Task<List<KnownBuyer>> LoadKnownBuyersForCompanyAsync(int companyPartyInfoId, CancellationToken ct)
        {
            var partyRows = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => sb.SupplierId == companyPartyInfoId && sb.BuyerId == p.PartyInfoId))
                .Select(p => new { p.CompanyName, p.TIN })
                .Take(200)
                .ToListAsync(ct);

            var publicRows = await _context.PublicCustomers
                .Where(pc => _context.SupplierBuyers.Any(sb => sb.SupplierId == companyPartyInfoId && sb.PublicCustomerId == pc.PublicCustomerId))
                .Select(pc => new { pc.CompanyName, pc.TIN })
                .Take(200)
                .ToListAsync(ct);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var buyers = new List<KnownBuyer>();

            void Add(string? name, string? tin)
            {
                if (string.IsNullOrWhiteSpace(tin) || !seen.Add(tin)) return;
                buyers.Add(new KnownBuyer(name ?? string.Empty, tin));
            }

            foreach (var r in partyRows) Add(r.CompanyName, r.TIN);
            foreach (var r in publicRows) Add(r.CompanyName, r.TIN);

            return buyers;
        }
    }
}
