using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.InputModel;
using eInvWorld.Pages.Invoices;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier,Buyer")]
    public class InvoiceDetails2Model : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceDetailsModel> _logger;
        private readonly QRCodeGeneratorService _qrCodeService;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILHDNApiService _lhdnApiService;

        public InvoiceHeader? InvoiceDetail { get; set; }
        public List<InvoiceLine> InvoiceLines { get; set; } = new();

        public string? InvoiceTypeDescription { get; set; }
        public DropdownHelper DropdownHelper { get; }
        public List<InvoiceHistory> InvoiceHistories { get; set; } = new();
        public string FullCurrencyName { get; set; } = "Malaysian Ringgit"; // Default fallback
        public string StatusName => InvoiceDetail?.InternalStatus?.Name ?? "Unknown";
        public string DisplayStatusName => InvoiceDetail?.LHDNStatus?.Name ?? InvoiceDetail?.InternalStatus?.Name ?? "";
        public string? QRCodeBase64 { get; set; }

        public InvoiceDetails2Model(
            ApplicationDbContext context,
            ILogger<InvoiceDetailsModel> logger,
            QRCodeGeneratorService qrCodeService,
            DropdownHelper dropdownHelper,
            IPdfGeneratorService pdfGeneratorService,
            UserManager<ApplicationUser> userManager,
            ILHDNApiService lhdnApiService
        )
        {
            _context = context;
            _logger = logger;
            _qrCodeService = qrCodeService;
            DropdownHelper = dropdownHelper;
            _pdfGeneratorService = pdfGeneratorService;
            _userManager = userManager;
            _lhdnApiService = lhdnApiService;
        }

        public async Task<IActionResult> OnGetAsync(string uuid, bool fromEmail = false)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                _logger.LogError("Invoice UUID or Invoice No is missing.");
                return NotFound();
            }

            // Check if the input is a UUID or an InvoiceNo
            bool isUuid = _context.InvoiceHeaders.Any(i => i.UUID == uuid);

            if (isUuid)
            {
                // Query using UUID (for Sent and Received invoices)
                InvoiceDetail = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)  //  Load Supplier (PartyInfo)
                        .ThenInclude(s => s.State)
                    .Include(i => i.Supplier)
                        .ThenInclude(s => s.Country)
                    .Include(i => i.Customer)  //  Load Buyer (PartyInfo)
                        .ThenInclude(c => c.State)
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.Country)
                    .Include(i => i.PublicCustomer) // 🔥 HYBRID FIX: Include PublicCustomer
                    .ThenInclude(p => p!.State)
                    .Include(i => i.PublicCustomer)
                            .ThenInclude(p => p!.Country)
                    .Include(i => i.InvoiceLines)
                        .ThenInclude(il => il.InvoiceTaxes)
                    .Include(i => i.LHDNStatus)
                    .Include(i => i.InternalStatus)
                    .FirstOrDefaultAsync(i => i.UUID == uuid);
            }
            else
            {
                // Query using InvoiceNo (for Draft invoices)
                InvoiceDetail = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                        .ThenInclude(s => s.State)
                    .Include(i => i.Supplier)
                        .ThenInclude(s => s.Country)
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.State)
                    .Include(i => i.Customer)
                        .ThenInclude(c => c.Country)
                    .Include(i => i.PublicCustomer) // 🔥 HYBRID FIX: Include PublicCustomer
                            .ThenInclude(p => p!.State)
                    .Include(i => i.PublicCustomer)
                            .ThenInclude(p => p!.Country)
                    .Include(i => i.InvoiceLines)
                        .ThenInclude(il => il.InvoiceTaxes)
                    .Include(i => i.LHDNStatus)
                    .Include(i => i.InternalStatus)
                    .FirstOrDefaultAsync(i => i.InvoiceNo == uuid); // Using uuid as InvoiceNo
            }

            if (InvoiceDetail == null)
            {
                _logger.LogError($"Invoice not found for UUID/InvoiceNo: {uuid}");
                return NotFound();
            }

            // IDOR guard: only a party to the invoice (Supplier/Customer/PublicCustomer) or an Admin may view it.
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, InvoiceDetail.InvoiceNo))
            {
                _logger.LogWarning("InvoiceDetails2 view denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, InvoiceDetail.InvoiceNo);
                return Forbid();
            }

            if (!string.IsNullOrEmpty(InvoiceDetail.Currency))
            {
                var currencyEntry = await _context.CurrencyCodes
                    .FirstOrDefaultAsync(c => c.Code == InvoiceDetail.Currency);

                if (currencyEntry != null)
                {
                    FullCurrencyName = currencyEntry.Currency;
                }
            }

            // ✅ Generate QR Code using UUID and LongId
            if (!string.IsNullOrEmpty(InvoiceDetail.UUID) && !string.IsNullOrEmpty(InvoiceDetail.LongId))
            {
                QRCodeBase64 = _qrCodeService.GenerateQRCodeBase64(InvoiceDetail.UUID, InvoiceDetail.LongId);
            }

            InvoiceLines = InvoiceDetail.InvoiceLines.ToList();

            // ✅ Load invoice history (with Malaysia time)
            var histories = await _context.InvoiceHistories
                .Where(h => h.InvoiceNo == InvoiceDetail.InvoiceNo)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();

            InvoiceHistories = histories.Select(h => new InvoiceHistory
            {
                Id = h.Id,
                InvoiceNo = h.InvoiceNo,
                Action = h.Action,
                PerformedBy = h.PerformedBy,
                Remarks = h.Remarks,
                Timestamp = DateTimeHelper.ToMalaysiaTime(h.Timestamp)
            }).ToList();

            // Map the Document Type Code to its Description
            var typeEntry = await _context.EInvoiceTypes
                .FirstOrDefaultAsync(t => t.Code == InvoiceDetail.DocTypeCode);
            InvoiceTypeDescription = typeEntry?.Description ?? InvoiceDetail.DocTypeCode;


            // 1. Fetch all active tax types from the database
            var taxTypes = await _context.TaxTypes.Where(t => t.IsActive).ToListAsync();

            // 2. Loop through each line and tax to map the description
            foreach (var line in InvoiceDetail.InvoiceLines)
            {
                if (line.InvoiceTaxes != null)
                {
                    foreach (var tax in line.InvoiceTaxes)
                    {
                        // Map the code to the description, fallback to the code if not found
                        tax.TaxCategoryDescription = taxTypes
                            .FirstOrDefault(t => t.Code == tax.TaxCategory)?.Description
                            ?? tax.TaxCategory;
                    }
                }
            }

            // Web Preview Fix: Point to the streaming API
            if (!string.IsNullOrEmpty(InvoiceDetail.Supplier?.LogoPath) && InvoiceDetail.Supplier.LogoPath.Contains("E:\\"))
            {
                // Extract just the file name (e.g., "12345_logo_xyz.png")
                var fileName = System.IO.Path.GetFileName(InvoiceDetail.Supplier.LogoPath);

                // Change the path in-memory to hit your new controller
                InvoiceDetail.Supplier.LogoPath = $"/api/image/logo?fileName={fileName}";
            }

            return Page();
        }

        public async Task<IActionResult> OnGetExportHistoryAsync(string invoiceNo)
        {
            if (string.IsNullOrEmpty(invoiceNo))
            {
                return BadRequest("Invoice number is required.");
            }

            // IDOR guard: restrict history export to invoices the user is allowed to access.
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, invoiceNo))
            {
                _logger.LogWarning("ExportHistory denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, invoiceNo);
                return Forbid();
            }

            var histories = await _context.InvoiceHistories
                .Where(h => h.InvoiceNo == invoiceNo)
                .OrderBy(h => h.Timestamp)
                .ToListAsync();

            // Convert all timestamps to Malaysia time
            InvoiceHistories = histories.Select(h => new InvoiceHistory
            {
                Id = h.Id,
                InvoiceNo = h.InvoiceNo,
                Action = h.Action,
                PerformedBy = h.PerformedBy,
                Remarks = h.Remarks,
                Timestamp = DateTimeHelper.ToMalaysiaTime(h.Timestamp)
            }).ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Action,PerformedBy,Remarks");

            foreach (var h in histories)
            {
                var timestamp = DateTimeHelper.ToMalaysiaTime(h.Timestamp).ToString("yyyy-MM-dd HH:mm:ss");
                var remarks = h.Remarks?.Replace("\"", "\"\"");
                csv.AppendLine($"\"{timestamp}\",\"{h.Action}\",\"{h.PerformedBy}\",\"{remarks}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"InvoiceHistory_{invoiceNo}.csv");
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync(string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                _logger.LogWarning("DownloadPdf: invoiceNo is missing.");
                return BadRequest("Invoice number is required.");
            }

            // IDOR guard: only let the user download invoices belonging to their company (or admins).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, invoiceNo))
            {
                _logger.LogWarning("DownloadPdf denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, invoiceNo);
                return Forbid();
            }

            try
            {
                // Generate PDF and get file path
                string pdfPath = await _pdfGeneratorService.GeneratePdfAsync(invoiceNo);

                if (!System.IO.File.Exists(pdfPath))
                {
                    _logger.LogError($"PDF file not found at {pdfPath}");
                    return NotFound("PDF file not generated.");
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
                return File(pdfBytes, "application/pdf", $"{invoiceNo}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to generate PDF for {invoiceNo}");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }

        // PUT /Invoices/InvoiceDetails2?handler=RejectDocument (Direct API like InvoiceLists)
        public async Task<IActionResult> OnPutRejectDocumentAsync(string documentId, string rejectionReason, string tin)
        {
            _logger.LogInformation("🚀 InvoiceDetails2 RejectDocument handler called with documentId: {documentId}, reason: {rejectionReason}, frontend tin: {tin}", documentId, rejectionReason, tin);

            // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceByUuidAsync(User, _context, documentId))
            {
                _logger.LogWarning("RejectDocument denied: user {User} cannot access document {DocumentId}.", User.Identity?.Name, documentId);
                return new JsonResult(new { success = false, message = "You are not authorized to reject this document." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(rejectionReason))
                {
                    _logger.LogWarning("❌ BadRequest: Document ID or rejection reason is missing.");
                    return BadRequest("Document ID and rejection reason are required.");
                }

                // Get the current user's TIN for the API call (ignore frontend TIN)
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("❌ No logged-in user found.");
                    return StatusCode(401, "User not logged in.");
                }

                var userTin = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userTin))
                {
                    _logger.LogError("❌ No TIN found for user {userId}.", user.Id);
                    return StatusCode(500, "User's TIN is missing.");
                }

                _logger.LogInformation("🔑 Using logged-in user's TIN for LHDN API: {userTin} (instead of frontend TIN: {tin})", userTin, tin);

                // Call LHDN API to reject the document using user's TIN
                _logger.LogInformation("📡 Calling LHDN RejectDocument API for document {documentId} with user TIN {userTin}...", documentId, userTin);
                string apiResponse;
                try
                {
                    apiResponse = await _lhdnApiService.RejectDocumentAsync(documentId, rejectionReason, userTin);
                    _logger.LogInformation("✅ LHDN API Response: {response}", apiResponse);
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "❌ LHDN API call failed for document {documentId} with TIN {userTin}", documentId, userTin);
                    return StatusCode(500, $"Failed to reject document in LHDN API: {apiEx.Message}");
                }

                _logger.LogInformation("✅ Document {documentId} successfully rejected in LHDN API", documentId);
                var history = new InvoiceHistory
                {
                    InvoiceNo = documentId, // Or the appropriate InvoiceNo field
                    Action = "RequestReject",
                    PerformedBy = user.UserName ?? "",
                    Remarks = $"Rejection requested. Reason: {rejectionReason}",
                    Timestamp = DateTime.UtcNow
                };
                _context.InvoiceHistories.Add(history);
                await _context.SaveChangesAsync();

                return new JsonResult(new { message = "Document rejection successfully processed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error rejecting document with ID: {DocumentId}", documentId);
                return StatusCode(500, "An error occurred while rejecting the document.");
            }
        }

        // PUT /Invoices/InvoiceDetails2?handler=CancelDocument (Direct API like InvoiceLists)
        public async Task<IActionResult> OnPutCancelDocumentAsync(string documentId, string cancellationReason, string tin)
        {
            _logger.LogInformation("🚀 InvoiceDetails2 CancelDocument handler called with documentId: {documentId}, reason: {cancellationReason}, tin: {tin}", documentId, cancellationReason, tin);

            // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceByUuidAsync(User, _context, documentId))
            {
                _logger.LogWarning("CancelDocument denied: user {User} cannot access document {DocumentId}.", User.Identity?.Name, documentId);
                return new JsonResult(new { success = false, message = "You are not authorized to cancel this document." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(cancellationReason))
                {
                    _logger.LogWarning("❌ BadRequest: Document ID or cancellation reason is missing.");
                    return BadRequest("Document ID and cancellation reason are required.");
                }

                // Get the current user's TIN for the API call
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("❌ No logged-in user found.");
                    return StatusCode(401, "User not logged in.");
                }

                var userTin = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userTin))
                {
                    _logger.LogError("❌ No TIN found for user {userId}.", user.Id);
                    return StatusCode(500, "User's TIN is missing.");
                }

                // Call LHDN API to cancel document
                _logger.LogInformation($"📡 Calling LHDN CancelDocument API for document {documentId} with user TIN {userTin}...");
                string response = await _lhdnApiService.CancelDocumentAsync(documentId, cancellationReason, userTin);
                _logger.LogInformation($"✅ LHDN API cancellation successful for document {documentId}");
                var history = new InvoiceHistory
                {
                    InvoiceNo = documentId,
                    Action = "Cancelled",
                    PerformedBy = user.UserName ?? "",
                    Remarks = $"Cancellation requested. Reason: {cancellationReason}",
                    Timestamp = DateTime.UtcNow
                };
                _context.InvoiceHistories.Add(history);
                await _context.SaveChangesAsync();
                return new JsonResult(new { message = "Document cancellation successfully processed.", response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error canceling document with ID: {DocumentId}", documentId);
                return StatusCode(500, "An error occurred while canceling the document.");
            }
        }
    }
}
