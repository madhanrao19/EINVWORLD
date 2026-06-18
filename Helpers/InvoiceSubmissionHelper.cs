using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.JsonModels;
using eInvWorld.Models.Audit;
using eInvWorld.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using eInvWorld.Models.Document;

namespace EINVWORLD.Helpers
{
    public class InvoiceSubmissionHelper
    {
        private readonly ApplicationDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ILHDNApiService _lhdnApiService;
        private readonly IJsonFileService _jsonFileService;
        private readonly ILogger<InvoiceSubmissionHelper> _logger;

        public InvoiceSubmissionHelper(
            ApplicationDbContext context,
            ITokenService tokenService,
            ILHDNApiService lhdnApiService,
            IJsonFileService jsonFileService,
            ILogger<InvoiceSubmissionHelper> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _lhdnApiService = lhdnApiService;
            _jsonFileService = jsonFileService;
            _logger = logger;
        }

        public async Task<(bool success, string message)> SubmitInvoiceAsync(string invoiceNo, string submittedBy = "System")
        {
            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

            if (invoice == null)
                return (false, $"Invoice {invoiceNo} not found.");

            if (invoice.InternalStatusId != "Draft")
                return (false, $"Invoice {invoiceNo} is not in Draft status.");

            var jsonPath = _jsonFileService.GetExistingFilePath(invoiceNo);
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                return (false, $"Draft JSON for invoice {invoiceNo} does not exist.");

            var invoiceJson = await File.ReadAllTextAsync(jsonPath);
            var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
            var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

            var documents = new List<eInvWorld.Models.JsonModels.Documents>
            {
                new eInvWorld.Models.JsonModels.Documents
                {
                    Format = "JSON",
                    DocumentHash = documentHash,
                    CodeNumber = invoiceNo,
                    Document = encodedDocument
                }
            };

            string? tin = TinHelper.ResolveSubmitterTin(invoice);

            if (string.IsNullOrWhiteSpace(tin))
                return (false, "Unable to determine taxpayer TIN.");

            string accessToken = await _tokenService.GetAccessTokenForTIN(tin);
            var apiResponseJson = await _lhdnApiService.SubmitDocumentsAsync(documents);
            _logger.LogInformation("📨 API response: {Response}", apiResponseJson);

            var apiResponse = JsonSerializer.Deserialize<SuccessSubmit>(apiResponseJson);
            var accepted = apiResponse?.acceptedDocuments?.FirstOrDefault();

            if (accepted == null || string.IsNullOrEmpty(accepted.uuid))
                return (false, $"Submission accepted but UUID missing for invoice {invoiceNo}.");

            // ✅ Save to DB
            invoice.UUID = accepted.uuid;
            invoice.SubmissionID = apiResponse?.submissionUID;
            invoice.LHDNStatusId = "Submitted";
            invoice.InternalStatusId = "Submitted";
            invoice.DateTimeReceived = GetMYTime();
            invoice.LastUpdated = GetMYTime();
            invoice.UpdatedBy = submittedBy;

            _context.InvoiceHeaders.Update(invoice);
            _context.InvoiceHistories.Add(new InvoiceHistory
            {
                InvoiceNo = invoiceNo,
                Action = "Submitted",
                Timestamp = GetMYTime(),
                PerformedBy = submittedBy,
                Remarks = $"Submitted via helper. UUID: {accepted.uuid}"
            });

            await _context.SaveChangesAsync();
            await Task.Delay(3000);

            // 🔁 Try polling
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    var docStatus = await _lhdnApiService.GetDocumentDetailsAsync(accepted.uuid, accessToken);
                    if (docStatus != null)
                    {
                        invoice.LHDNStatusId = docStatus.status;
                        invoice.InternalStatusId = docStatus.status;
                        invoice.LastUpdated = GetMYTime();

                        _context.InvoiceHeaders.Update(invoice);
                        _context.InvoiceHistories.Add(new InvoiceHistory
                        {
                            InvoiceNo = invoiceNo,
                            Action = "StatusSync",
                            Timestamp = GetMYTime(),
                            PerformedBy = submittedBy,
                            Remarks = $"Polled status from LHDN: {docStatus.status}"
                        });

                        await _context.SaveChangesAsync();
                        break;
                    }
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("404"))
                {
                    await Task.Delay(2000);
                }
            }

            _jsonFileService.MoveToStatusFolder(invoice.InvoiceNo, invoice.LHDNStatusId);
            return (true, $"Invoice {invoice.InvoiceNo} submitted successfully. UUID: {accepted.uuid}");
        }

        private DateTime GetMYTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
        }
    }
}
