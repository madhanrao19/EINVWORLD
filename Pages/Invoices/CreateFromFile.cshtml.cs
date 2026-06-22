using System.Security.Claims;
using eInvWorld.Data;
using EINVWORLD.Services.Assistant;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.DocumentCapture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Invoices
{
    /// <summary>
    /// AI Document Capture (Phase 1): upload a digital invoice PDF, extract its text, and turn it into a
    /// reviewed invoice <em>suggestion</em> via the local LLM — draft-only, never auto-submitted. Reuses
    /// the same SuggestInvoiceAsync + ReviewSuggestion + known-buyer grounding as the AI Assistant, so the
    /// output is validated against real LHDN codes and the user's real customers before they act on it.
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class CreateFromFileModel : PageModel
    {
        private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

        private readonly IEInvoiceAssistantService _assistant;
        private readonly IDocumentTextExtractor _extractor;
        private readonly DocumentCaptureOptions _options;
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly ILogger<CreateFromFileModel> _logger;

        public CreateFromFileModel(
            IEInvoiceAssistantService assistant,
            IDocumentTextExtractor extractor,
            DocumentCaptureOptions options,
            ApplicationDbContext context,
            IAuditService audit,
            ILogger<CreateFromFileModel> logger)
        {
            _assistant = assistant;
            _extractor = extractor;
            _options = options;
            _context = context;
            _audit = audit;
            _logger = logger;
        }

        /// <summary>Capture needs both its own switch and the AI assistant (which does the extraction → suggestion).</summary>
        public bool Enabled => _options.Enabled && _assistant.IsEnabled;
        public int MaxFileSizeMb => _options.MaxFileSizeMb;

        [BindProperty]
        public IFormFile? Upload { get; set; }

        public string? FileName { get; private set; }
        public string? ExtractedText { get; private set; }
        public string? SuggestionJson { get; private set; }
        public SuggestionReview? Review { get; private set; }
        public string? ErrorText { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!Enabled)
            {
                ErrorText = "AI Document Capture is disabled. Enable DocumentCapture and the AI assistant in configuration.";
                return Page();
            }

            if (Upload is null || Upload.Length == 0)
            {
                ErrorText = "Please choose a PDF file to upload.";
                return Page();
            }

            if (Upload.Length > _options.MaxFileSizeMb * 1024L * 1024L)
            {
                ErrorText = $"File is too large (limit {_options.MaxFileSizeMb} MB).";
                return Page();
            }

            if (!Upload.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ErrorText = "Only PDF files are supported in this phase (scanned-image OCR is a later phase).";
                return Page();
            }

            FileName = Path.GetFileName(Upload.FileName);

            // Read into memory and verify the real file signature (defence against a renamed non-PDF).
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await Upload.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }
            if (bytes.Length < PdfMagic.Length || !bytes.AsSpan(0, PdfMagic.Length).SequenceEqual(PdfMagic))
            {
                ErrorText = "That file is not a valid PDF.";
                return Page();
            }

            var text = _extractor.ExtractPdfText(bytes, _options.MaxPages);
            if (string.IsNullOrWhiteSpace(text))
            {
                ErrorText = "No text could be extracted — this looks like a scanned image. Image OCR is not supported yet; " +
                            "please upload a digital (text-based) PDF or capture the invoice via the AI Assistant.";
                return Page();
            }
            ExtractedText = text;

            // Ground the suggestion on the user's real customers, exactly like the AI Assistant does.
            var knownBuyers = await LoadKnownBuyersAsync(ct);

            var result = await _assistant.SuggestInvoiceAsync(text, knownBuyers, ct);
            if (!result.Ok)
            {
                ErrorText = result.Error;
                return Page();
            }

            SuggestionJson = result.Content;
            Review = _assistant.ReviewSuggestion(result.Content, knownBuyers.Select(b => b.Tin).ToList());

            await _audit.WriteAsync("DocumentCaptured", new AuditEntry
            {
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { file = FileName, bytes = bytes.Length })
            });

            return Page();
        }

        /// <summary>
        /// The customers the current user may invoice — registered buyers (PartyInfo) and public customers
        /// (PublicCustomer) linked via SupplierBuyers. Same set the Create Invoice form offers.
        /// </summary>
        private async Task<List<KnownBuyer>> LoadKnownBuyersAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return new();

            var companyIds = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfoId)
                .ToListAsync(ct);
            if (companyIds.Count == 0) return new();

            var partyRows = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.BuyerId == p.PartyInfoId))
                .Select(p => new { p.CompanyName, p.TIN })
                .Take(200)
                .ToListAsync(ct);

            var publicRows = await _context.PublicCustomers
                .Where(pc => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.PublicCustomerId == pc.PublicCustomerId))
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
