using System.Linq;
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
        public int MaxFilesPerBulkUpload => _options.MaxFilesPerBulkUpload;

        public List<(int PartyInfoId, string Name)> MemberCompanies { get; private set; } = new();
        public List<SmartCaptureDocument> Documents { get; private set; } = new();

        /// <summary>Stage 3: accepts one or many files from the same &lt;input multiple&gt; field — a
        /// single upload is just a batch of one. Each file goes through the exact same per-file
        /// validation/quota/storage path (SmartCaptureDocumentService.UploadAsync) as before; nothing about
        /// that path changed for bulk.</summary>
        [BindProperty]
        public List<IFormFile>? Uploads { get; set; }

        [BindProperty]
        public int CompanyPartyInfoId { get; set; }

        public string? ErrorText { get; private set; }
        public string? SuccessText { get; private set; }

        /// <summary>Per-file failures from the most recent bulk upload ("invoice3.pdf: file too large"),
        /// shown alongside SuccessText so a partially-successful batch is never silently incomplete.</summary>
        public List<string> BatchFailures { get; private set; } = new();

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

            var files = (Uploads ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
            if (files.Count == 0)
            {
                ErrorText = "Please choose at least one file to upload.";
                await LoadAsync(ct);
                return Page();
            }

            if (files.Count > _options.MaxFilesPerBulkUpload)
            {
                ErrorText = $"Too many files in one batch (limit {_options.MaxFilesPerBulkUpload}). Please upload in smaller batches.";
                await LoadAsync(ct);
                return Page();
            }

            var succeeded = 0;
            foreach (var file in files)
            {
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms, ct);
                    bytes = ms.ToArray();
                }

                var result = await _documents.UploadAsync(bytes, file.FileName, file.ContentType, CompanyPartyInfoId, userId, ct);
                if (result.Ok)
                    succeeded++;
                else
                    BatchFailures.Add($"{file.FileName}: {result.UserMessage}");
            }

            if (succeeded > 0)
                SuccessText = files.Count == 1
                    ? "Document uploaded and queued for processing."
                    : $"{succeeded} of {files.Count} documents uploaded and queued for processing.";
            if (BatchFailures.Count > 0 && succeeded == 0)
                ErrorText = "No documents could be uploaded.";

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
