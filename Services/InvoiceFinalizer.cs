using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eInvWorld.Services
{
    /// <summary>
    /// Shared single-invoice finalizer (PDF generation + validation email). See
    /// <see cref="IInvoiceFinalizer"/> for the contract. Flag updates are written with
    /// <c>ExecuteUpdate</c> so they never trip the InvoiceHeader rowversion check, and the email
    /// send is claimed atomically (UPDATE ... WHERE IsValidationEmailSent = 0) so concurrent
    /// finalizers — the interactive submit flow plus the background loops — cannot double-send.
    /// </summary>
    public class InvoiceFinalizer : IInvoiceFinalizer
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfGeneratorService _pdfService;
        private readonly IEInvoiceNotificationService _notificationService;
        private readonly FilePathConfig _filePathConfig;
        private readonly ILogger<InvoiceFinalizer> _logger;

        public InvoiceFinalizer(
            ApplicationDbContext context,
            IPdfGeneratorService pdfService,
            IEInvoiceNotificationService notificationService,
            IOptions<FilePathConfig> filePathConfig,
            ILogger<InvoiceFinalizer> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _notificationService = notificationService;
            _filePathConfig = filePathConfig.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<InvoiceFinalizeResult> FinalizeInvoiceAsync(string invoiceNo, string performedBy = "Finalizer", CancellationToken cancellationToken = default)
        {
            var invoice = await _context.InvoiceHeaders
                .AsNoTracking()
                .Include(i => i.Customer)
                .Include(i => i.Supplier)
                .Include(i => i.PublicCustomer)
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo, cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("Finalize skipped: invoice {InvoiceNo} not found.", invoiceNo);
                return new InvoiceFinalizeResult();
            }

            // Preconditions: only a fully-validated invoice (status, QR LongId, validation timestamp)
            // gets a PDF and the validation email. Anything else is left for a later sync/finalizer pass.
            if (invoice.LHDNStatusId != "Valid" || string.IsNullOrWhiteSpace(invoice.LongId) || invoice.DateTimeValidated == null)
            {
                return new InvoiceFinalizeResult();
            }

            bool pdfGenerated = false;
            bool emailSent = false;
            string pdfPath = Path.Combine(_filePathConfig.GeneratedPdfFolder, $"{invoice.InvoiceNo}.pdf");

            // 1. Generate the PDF if it's missing (flag not set, or the file was deleted on disk).
            if (!invoice.IsPdfGenerated || !File.Exists(pdfPath))
            {
                try
                {
                    _logger.LogInformation("📄 Generating PDF for invoice {InvoiceNo}...", invoice.InvoiceNo);
                    await _pdfService.GeneratePdfAsync(invoice.InvoiceNo);

                    var generatedAt = DateTime.Now;
                    await _context.InvoiceHeaders
                        .Where(i => i.InvoiceNo == invoiceNo)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.IsPdfGenerated, true)
                            .SetProperty(i => i.PdfGeneratedAt, generatedAt), cancellationToken);

                    _context.InvoiceHistories.Add(new InvoiceHistory
                    {
                        InvoiceNo = invoice.InvoiceNo,
                        Action = "PDFGenerated",
                        PerformedBy = performedBy,
                        Remarks = "Validated-invoice PDF generated.",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync(cancellationToken);

                    pdfGenerated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to generate PDF for {InvoiceNo}", invoice.InvoiceNo);
                }
            }

            if (invoice.IsValidationEmailSent && !File.Exists(pdfPath) && !pdfGenerated)
            {
                _logger.LogWarning("⚠️ PDF for invoice {InvoiceNo} missing after email was already sent. No resend.", invoice.InvoiceNo);
            }

            // 2. Send the validation email once the PDF exists on disk. The claim below flips
            //    IsValidationEmailSent to true only if it is still false, so exactly one concurrent
            //    finalizer wins the right to send; the flag is rolled back if the send then fails.
            if (!invoice.IsValidationEmailSent && File.Exists(pdfPath))
            {
                var recipients = string.Join(", ", new[]
                {
                    invoice.Customer?.Email ?? invoice.PublicCustomer?.Email,
                    invoice.Supplier?.Email
                }.Where(e => !string.IsNullOrWhiteSpace(e)));

                var sentAt = DateTime.Now;
                var claimed = await _context.InvoiceHeaders
                    .Where(i => i.InvoiceNo == invoiceNo && !i.IsValidationEmailSent)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.IsValidationEmailSent, true)
                        .SetProperty(i => i.ValidationEmailSentAt, sentAt)
                        .SetProperty(i => i.ValidationEmailSentTo, recipients), cancellationToken);

                if (claimed == 1)
                {
                    try
                    {
                        _logger.LogInformation("📧 Sending validation email for invoice {InvoiceNo}", invoice.InvoiceNo);
                        await _notificationService.SendValidatedNotificationEmail(
                            invoice.Customer?.CompanyName ?? invoice.PublicCustomer?.CompanyName ?? "Customer",
                            invoice.Customer,
                            invoice.Supplier,
                            invoice.InvoiceNo,
                            invoice.IssueDate ?? DateTime.Now,
                            invoice.DateTimeValidated ?? DateTime.Now,
                            invoice.PublicCustomer);

                        _context.InvoiceHistories.Add(new InvoiceHistory
                        {
                            InvoiceNo = invoice.InvoiceNo,
                            Action = "EmailSent",
                            PerformedBy = performedBy,
                            Remarks = recipients,
                            Timestamp = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync(cancellationToken);

                        emailSent = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Failed to send validation email for {InvoiceNo}; releasing the send claim for retry.", invoice.InvoiceNo);

                        // Roll the claim back so a later finalizer pass retries the send. Deliberately
                        // not cancellable — the claim must be released even during shutdown.
                        try
                        {
                            await _context.InvoiceHeaders
                                .Where(i => i.InvoiceNo == invoiceNo)
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(i => i.IsValidationEmailSent, false)
                                    .SetProperty(i => i.ValidationEmailSentAt, (DateTime?)null)
                                    .SetProperty(i => i.ValidationEmailSentTo, (string?)null), CancellationToken.None);
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogError(rollbackEx, "❌ Failed to release the email claim for {InvoiceNo}; the validation email will not be retried automatically.", invoice.InvoiceNo);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Validation email for {InvoiceNo} already claimed by a concurrent finalizer; skipping.", invoice.InvoiceNo);
                }
            }

            var completed = (invoice.IsPdfGenerated || pdfGenerated)
                            && (invoice.IsValidationEmailSent || emailSent)
                            && File.Exists(pdfPath);

            return new InvoiceFinalizeResult
            {
                Eligible = true,
                PdfGenerated = pdfGenerated,
                EmailSent = emailSent,
                Completed = completed
            };
        }
    }
}
