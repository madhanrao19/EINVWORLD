using System.Security.Claims;
using eInvWorld.Data;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Services.SmartCapture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Invoices
{
    /// <summary>
    /// Smart Capture (Stage 1): upload a supplier invoice, then track it through async OCR/LLM extraction
    /// via the existing SyncJobs queue. Sits alongside the original synchronous CreateFromFile page — that
    /// page is left untouched as a fallback (see PR notes / DEPLOY-NOTES.md for the rollback rationale).
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class SmartCaptureModel : PageModel
    {
        private readonly SmartCaptureDocumentService _documents;
        private readonly SmartCaptureOptions _options;
        private readonly ApplicationDbContext _context;

        public SmartCaptureModel(SmartCaptureDocumentService documents, SmartCaptureOptions options, ApplicationDbContext context)
        {
            _documents = documents;
            _options = options;
            _context = context;
        }

        public bool Enabled => _options.Enabled;
        public int MaxFileSizeMb => _options.MaxFileSizeMb;
        public string[] AllowedExtensions => _options.AllowedExtensions;

        public List<(int PartyInfoId, string Name)> MemberCompanies { get; private set; } = new();
        public List<SmartCaptureDocument> Documents { get; private set; } = new();

        [BindProperty]
        public IFormFile? Upload { get; set; }

        [BindProperty]
        public int CompanyPartyInfoId { get; set; }

        public string? ErrorText { get; private set; }
        public string? SuccessText { get; private set; }

        public async Task OnGetAsync(CancellationToken ct)
        {
            await LoadAsync(ct);
        }

        public async Task<IActionResult> OnPostUploadAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                ErrorText = "Your session could not be verified. Please sign in again.";
                await LoadAsync(ct);
                return Page();
            }

            if (Upload is null || Upload.Length == 0)
            {
                ErrorText = "Please choose a file to upload.";
                await LoadAsync(ct);
                return Page();
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await Upload.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var result = await _documents.UploadAsync(bytes, Upload.FileName, Upload.ContentType, CompanyPartyInfoId, userId, ct);
            if (!result.Ok)
            {
                ErrorText = result.UserMessage;
                await LoadAsync(ct);
                return Page();
            }

            SuccessText = "Document uploaded and queued for processing.";
            await LoadAsync(ct);
            return Page();
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var companyIds = await _documents.GetMemberCompanyIdsAsync(userId, ct);

            var companyRows = await _context.PartyInfos
                .Where(p => companyIds.Contains(p.PartyInfoId))
                .Select(p => new { p.PartyInfoId, p.CompanyName })
                .ToListAsync(ct);
            MemberCompanies = companyRows.Select(p => (p.PartyInfoId, p.CompanyName ?? $"Company #{p.PartyInfoId}")).ToList();

            if (CompanyPartyInfoId == 0 && MemberCompanies.Count > 0)
                CompanyPartyInfoId = MemberCompanies[0].PartyInfoId;

            Documents = await _documents.ListForUserAsync(userId, ct);
        }
    }
}
