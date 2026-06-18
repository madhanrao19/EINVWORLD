using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.JsonModels;
using eInvWorld.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eInvWorld.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EInvoicingController : ControllerBase
    {
        private readonly ILHDNApiService _lhdnApiService;
        private readonly ILogger<EInvoicingController> _logger;
        private readonly TaxpayerValidationSettings _taxpayerValidationSettings;
        private readonly IConfiguration _configuration;
        private readonly QRCodeGeneratorService _qrCodeService;
        private readonly ApplicationDbContext _dbContext;



        public EInvoicingController(ILHDNApiService lhdnApiService, ILogger<EInvoicingController> logger, IConfiguration configuration,
                                    QRCodeGeneratorService qrCodeService,
                                    ApplicationDbContext dbContext,
                                    IOptions<TaxpayerValidationSettings> taxpayerValidationSettings)
        {
            _lhdnApiService = lhdnApiService ?? throw new ArgumentNullException(nameof(lhdnApiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _taxpayerValidationSettings = taxpayerValidationSettings.Value;
        }

        [HttpGet("validateTaxpayer/{tin}")]
        public async Task<IActionResult> ValidateTaxpayer(string tin, [FromQuery] string idType, [FromQuery] string idValue)
        {

            _logger.LogDebug("Received TIN: {Tin}, ID Type: {IdType}, ID Value: {IdValue}", tin, idType, idValue);

            // Use the values from appsettings.json or fallback to defaults
            tin ??= _taxpayerValidationSettings.DefaultTIN;
            idType ??= _taxpayerValidationSettings.DefaultIdType;
            idValue ??= _taxpayerValidationSettings.DefaultIdValue;

            _logger.LogDebug("Using TIN: {Tin}, ID Type: {IdType}, ID Value: {IdValue}", tin, idType, idValue);

            // Validate query parameters
            if (string.IsNullOrWhiteSpace(tin))
            {
                _logger.LogWarning("TIN parameter is null or empty.");
                return BadRequest("TIN parameter is required.");
            }

            if (string.IsNullOrWhiteSpace(idType))
            {
                _logger.LogWarning("idType parameter is null or empty.");
                return BadRequest("idType parameter is required.");
            }

            if (string.IsNullOrWhiteSpace(idValue))
            {
                _logger.LogWarning("idValue parameter is null or empty.");
                return BadRequest("idValue parameter is required.");
            }

            try
            {
                _logger.LogInformation("Validating taxpayer with TIN: {TIN}, ID Type: {IDType}, ID Value: {IDValue}", tin, idType, idValue);
                var result = await _lhdnApiService.ValidateTaxpayerAsync(tin, idType, idValue);
                _logger.LogInformation("Taxpayer validation successful for TIN: {TIN}.", tin);
                return Ok(result);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error while validating taxpayer with TIN: {TIN}.", tin);
                return StatusCode(500, $"HTTP request error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating taxpayer with TIN: {TIN}.", tin);
                return StatusCode(500, $"Error validating taxpayer: {ex.Message}");
            }
        }

        [HttpPost("submitDocuments")]
        public async Task<IActionResult> SubmitDocuments([FromBody] List<Models.JsonModels.Documents> documents, string accessToken)
        {
            if (documents == null || documents.Count == 0)
            {
                _logger.LogWarning("No documents provided for submission.");
                return BadRequest("At least one document is required for submission.");
            }

            try
            {
                _logger.LogInformation("Submitting documents.");
                var submissionResult = await _lhdnApiService.SubmitDocumentsAsync(documents);
                _logger.LogInformation("Documents submitted successfully. Now saving the invoice PDF...");

                foreach (var document in documents)
                {
                    // 🔹 Retrieve InvoiceNo from `InvoiceHeaders` using `codeNumber`
                    var invoice = await _dbContext.InvoiceHeaders
                        .FirstOrDefaultAsync(i => i.RefDocumentNo == document.CodeNumber);

                    if (invoice == null || string.IsNullOrEmpty(invoice.InvoiceNo))
                    {
                        _logger.LogWarning($"Invoice number not found for document: {document.CodeNumber}");
                        continue; // Skip saving PDF if no InvoiceNo
                    }

                    // 🔹 Save PDF with `InvoiceNo.pdf`
                    var filePath = Path.Combine(_configuration["FilePathConfig:GeneratedPdfFolder"] ?? string.Empty, $"{invoice.InvoiceNo}.pdf");

                    if (System.IO.File.Exists(filePath))
                    {
                        var pdfBytes = System.IO.File.ReadAllBytes(filePath);
                        var formFile = new FormFile(new MemoryStream(pdfBytes), 0, pdfBytes.Length, "file", $"{invoice.InvoiceNo}.pdf");
                        var invoiceLogger = LoggerFactory.Create(builder => builder.AddConsole())
                                                .CreateLogger<InvoiceController>();

                        var invoiceController = new InvoiceController(_configuration, invoiceLogger, _qrCodeService, _dbContext);
                        var pdfSaveResult = await invoiceController.SaveInvoiceToServer(formFile);

                        if (pdfSaveResult is ObjectResult objResult && objResult.StatusCode != 200)
                        {
                            _logger.LogWarning($"Failed to save PDF for Invoice {invoice.InvoiceNo}: {objResult.Value}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"PDF file for Invoice {invoice.InvoiceNo} does not exist. Skipping save.");
                    }
                }
                return Ok(submissionResult);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error while submitting documents.");
                return StatusCode(500, $"HTTP request error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting documents.");
                return StatusCode(500, $"Error submitting documents: {ex.Message}");
            }
        }
    }
}
