using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Invoices
{
    /// <summary>
    /// Bulk invoice import — validate-only. Upload a CSV/XLSX (one row per invoice line) and get a
    /// per-row validation report against the real LHDN reference codes. It does NOT create anything;
    /// it's a pre-entry check so errors are caught before invoices are keyed in. (Creating drafts from
    /// an import is a later phase that reuses the interactive draft path.)
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class BulkImportModel : PageModel
    {
        private const long MaxBytes = 10 * 1024 * 1024;

        private readonly IBulkInvoiceImportService _import;
        private readonly IAuditService _audit;

        public BulkImportModel(IBulkInvoiceImportService import, IAuditService audit)
        {
            _import = import;
            _audit = audit;
        }

        [BindProperty]
        public IFormFile? Upload { get; set; }

        public ImportPreview? Preview { get; private set; }
        public string? FileName { get; private set; }
        public string? ErrorText { get; private set; }

        public System.Collections.Generic.IReadOnlyList<string> Columns => _import.TemplateColumns;

        public void OnGet() { }

        public IActionResult OnGetTemplate()
        {
            var bytes = _import.BuildTemplateXlsx();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "invoice-import-template.xlsx");
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (Upload is null || Upload.Length == 0)
            {
                ErrorText = "Please choose a CSV or XLSX file.";
                return Page();
            }
            if (Upload.Length > MaxBytes)
            {
                ErrorText = "File is too large (limit 10 MB).";
                return Page();
            }

            var name = Upload.FileName;
            var isCsv = name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase);
            var isXlsx = name.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase);
            if (!isCsv && !isXlsx)
            {
                ErrorText = "Only .csv and .xlsx files are supported.";
                return Page();
            }

            FileName = Path.GetFileName(name);

            using var ms = new MemoryStream();
            await Upload.CopyToAsync(ms, ct);
            ms.Position = 0;

            Preview = await _import.ParseAndValidateAsync(ms, isCsv, ct);

            await _audit.WriteAsync("BulkImportValidated", new AuditEntry
            {
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    file = FileName,
                    rows = Preview.TotalRows,
                    errorRows = Preview.ErrorRows
                })
            });

            return Page();
        }
    }
}
