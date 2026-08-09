using System.Security.Claims;
using System.Text.Json;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.SmartCapture;
using eInvWorld.Models.ViewModels;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Assistant;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.SmartCapture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eInvWorld.Pages.Invoices
{
    /// <summary>
    /// Reviews one Smart Capture document: shows live status while queued/processing, and once extraction
    /// finishes, shows the suggested fields plus the LHDN-aware review checklist (InvoiceSuggestionValidator
    /// — the exact same check CreateFromFile already runs). The user must explicitly pick a registered
    /// buyer and confirm the LHDN document type before a draft can be created; nothing is inferred silently.
    /// On confirm, creates the draft via the UNCHANGED InvoiceDraftService.SaveDraft and hands off to the
    /// normal InvoiceEdit page for full correction — the same path a manually-entered invoice takes.
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class SmartCaptureReviewModel : PageModel
    {
        private static readonly string[] ValidDocTypes = { "01", "02", "03", "04", "11", "12", "13", "14" };

        private readonly SmartCaptureDocumentService _documents;
        private readonly ApplicationDbContext _context;
        private readonly FilePathConfig _filePathConfig;
        private readonly InvoiceService _invoiceService;
        private readonly InvoiceDraftService _draftService;
        private readonly SmartCaptureCompanyHintService _hints;
        private readonly IAuditService _audit;
        private readonly ILogger<SmartCaptureReviewModel> _logger;

        public SmartCaptureReviewModel(
            SmartCaptureDocumentService documents,
            ApplicationDbContext context,
            IOptions<FilePathConfig> filePathConfig,
            InvoiceService invoiceService,
            InvoiceDraftService draftService,
            SmartCaptureCompanyHintService hints,
            IAuditService audit,
            ILogger<SmartCaptureReviewModel> logger)
        {
            _documents = documents;
            _context = context;
            _filePathConfig = filePathConfig.Value;
            _invoiceService = invoiceService;
            _draftService = draftService;
            _hints = hints;
            _audit = audit;
            _logger = logger;
        }

        public SmartCaptureDocument? Document { get; private set; }
        public InvoiceSuggestion? Suggestion { get; private set; }
        public List<SmartCaptureReviewItemDto> ReviewItems { get; private set; } = new();
        public bool ReviewHasErrors { get; private set; }

        /// <summary>True when the review checklist has neither errors nor warnings — the "nothing to
        /// look at" tier. Drives a condensed review presentation (checklist/raw-suggestion collapsed by
        /// default) instead of a change to the Confirm flow itself: the human still always clicks Confirm
        /// before a draft is created, matching Stage 1's explicit "never auto-decide doc type" rule.</summary>
        public bool IsFullyClean => !ReviewHasErrors && !ReviewItems.Any(i => i.Severity == "Warning");
        public List<(int PartyInfoId, string Name, string Tin)> KnownBuyers { get; private set; } = new();
        public string[] ValidDocumentTypes => ValidDocTypes;
        public string? ErrorText { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            Document = await _documents.GetOwnedAsync(id, userId, ct);
            if (Document is null) return NotFound();

            await LoadExtractionAsync(ct);
            return Page();
        }

        /// <summary>Tenant-scoped download of the original uploaded file — resolves the path server-side via
        /// SafePath after re-checking ownership; never trusts a client-supplied path.</summary>
        public async Task<IActionResult> OnGetDownloadAsync(int id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var document = await _documents.GetOwnedAsync(id, userId, ct);
            if (document is null) return NotFound();

            if (!SafePath.TryResolve(_filePathConfig.SmartCaptureFolder, out var fullPath,
                    document.CompanyPartyInfoId.ToString(), Path.GetFileName(document.InternalStorageReference)))
                return NotFound();

            if (!System.IO.File.Exists(fullPath)) return NotFound();

            await _audit.WriteAsync("SmartCaptureDocumentDownloaded", new AuditEntry
            {
                NewValueJson = JsonSerializer.Serialize(new { documentId = document.Id })
            }, ct);

            return PhysicalFile(fullPath, document.ContentType, document.OriginalFileName);
        }

        public async Task<IActionResult> OnPostConfirmAsync(
            int id, string confirmedDocTypeCode, int selectedBuyerPartyInfoId, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            Document = await _documents.GetOwnedAsync(id, userId, ct);
            if (Document is null) return NotFound();

            if (Document.Status != SmartCaptureDocumentStatus.ReviewRequired)
            {
                ErrorText = "This document is not ready for draft creation.";
                await LoadExtractionAsync(ct);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(confirmedDocTypeCode) || !ValidDocTypes.Contains(confirmedDocTypeCode.Trim()))
            {
                ErrorText = "Please confirm a valid LHDN document type before creating the draft.";
                await LoadExtractionAsync(ct);
                return Page();
            }

            await LoadExtractionAsync(ct); // populates KnownBuyers for the re-check below
            var buyer = KnownBuyers.FirstOrDefault(b => b.PartyInfoId == selectedBuyerPartyInfoId);
            if (buyer == default)
            {
                ErrorText = "Please select one of this company's registered buyers — it must match an existing customer.";
                return Page();
            }

            var supplierParty = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == Document.CompanyPartyInfoId, ct);
            var customerParty = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == buyer.PartyInfoId, ct);
            if (supplierParty is null || customerParty is null)
            {
                ErrorText = "Could not resolve the company or buyer record.";
                return Page();
            }

            var model = BuildInvoiceHeaderView(Suggestion, confirmedDocTypeCode.Trim(), supplierParty.PartyInfoId, customerParty.PartyInfoId);
            model.InvoiceNo = _invoiceService.GenerateNextInvoiceNumber();
            model.CalculateInvoiceTotals();

            var username = User.Identity?.Name ?? "System";
            var json = JsonSerializer.Serialize(model);
            var saved = _draftService.SaveDraft(model, username, supplierParty, customerParty, json, HttpContext.Session);
            if (!saved)
            {
                ErrorText = "Failed to create the draft invoice. Please try again.";
                return Page();
            }

            Document.Status = SmartCaptureDocumentStatus.DraftCreated;
            Document.ConfirmedDocTypeCode = confirmedDocTypeCode.Trim();
            Document.RelatedInvoiceHeaderInvoiceNo = model.InvoiceNo;
            Document.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Two concurrent confirms (double-click, retry-after-timeout) can both pass the
                // ReviewRequired check above and both create a draft via SaveDraft before either commits
                // this status update — the loser hits RowVersion's optimistic-concurrency conflict here.
                // The draft it made already exists (SaveDraft commits independently), so recover instead
                // of surfacing a raw 500: whichever request actually won gets to be the source of truth.
                var winner = await _documents.GetOwnedAsync(id, userId, ct);
                if (winner?.Status == SmartCaptureDocumentStatus.DraftCreated && !string.IsNullOrEmpty(winner.RelatedInvoiceHeaderInvoiceNo))
                    return RedirectToPage("/Invoices/InvoiceEdit", new { id = winner.RelatedInvoiceHeaderInvoiceNo });

                ErrorText = "This document was already processed by another request. Please refresh and try again.";
                return Page();
            }

            await _audit.WriteAsync("SmartCaptureDraftCreated", new AuditEntry
            {
                InvoiceNo = model.InvoiceNo,
                NewValueJson = JsonSerializer.Serialize(new { documentId = Document.Id, invoiceNo = model.InvoiceNo, docType = confirmedDocTypeCode })
            }, ct);

            // Learn from this confirmation for next time — advisory-only, best-effort. The draft already
            // exists at this point, so a failure here must never turn a successful Confirm into a 500.
            try
            {
                await _hints.RecordConfirmedAsync(
                    Document.CompanyPartyInfoId, confirmedDocTypeCode.Trim(), model.Currency, Suggestion?.TaxType, Suggestion?.TaxRatePercent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Smart Capture: failed to record company hint for document {DocumentId} — draft {InvoiceNo} was still created successfully.", Document.Id, model.InvoiceNo);
            }

            return RedirectToPage("/Invoices/InvoiceEdit", new { id = model.InvoiceNo });
        }

        private InvoiceHeaderView BuildInvoiceHeaderView(InvoiceSuggestion? suggestion, string docTypeCode, int supplierId, int customerId)
        {
            var model = new InvoiceHeaderView
            {
                DocTypeCode = docTypeCode,
                Currency = string.IsNullOrWhiteSpace(suggestion?.Currency) ? "MYR" : suggestion!.Currency!,
                IssueDate = DateTime.UtcNow,
                SupplierId = supplierId,
                CustomerId = customerId,
                InvoiceLines = (suggestion?.LineItems ?? new List<SuggestionLine>()).Select((line, idx) => new InvoiceLineView
                {
                    LineNumber = idx + 1,
                    ItemDescription = string.IsNullOrWhiteSpace(line.Description) ? "Item" : line.Description!,
                    UnitOfMeasure = "EA",
                    Quantity = line.Quantity ?? 1,
                    UnitPrice = line.UnitPrice ?? 0,
                    ClassificationCode = line.ClassificationCode ?? string.Empty,
                    Taxes = string.IsNullOrWhiteSpace(suggestion?.TaxType) ? new List<InvoiceTaxView>() : new List<InvoiceTaxView>
                    {
                        new() { TaxCategory = suggestion!.TaxType!, TaxPercentage = suggestion.TaxRatePercent ?? 0 }
                    }
                }).ToList()
            };
            return model;
        }

        private async Task LoadExtractionAsync(CancellationToken ct)
        {
            if (Document?.NormalizedExtractionJson is null) return;

            try
            {
                var payload = JsonSerializer.Deserialize<SmartCaptureExtractionPayload>(Document.NormalizedExtractionJson);
                if (payload is null) return;

                Suggestion = InvoiceSuggestionValidator.TryParse(payload.SuggestionJson);
                ReviewItems = payload.ReviewItems;
                ReviewHasErrors = payload.ReviewHasErrors;
            }
            catch (JsonException)
            {
                ErrorText = "The stored extraction result could not be read.";
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var companyIds = new List<int> { Document!.CompanyPartyInfoId };
            var partyRows = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.BuyerId == p.PartyInfoId))
                .Select(p => new { p.PartyInfoId, p.CompanyName, p.TIN })
                .Take(200)
                .ToListAsync(ct);
            KnownBuyers = partyRows.Select(p => (p.PartyInfoId, p.CompanyName ?? $"#{p.PartyInfoId}", p.TIN ?? string.Empty)).ToList();
        }
    }
}
