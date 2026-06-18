using System;
using System.IO;
using eInvWorld.Data;
using eInvWorld.Pages.Invoices;
using eInvWorld.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<InvoiceController> _logger;
        private readonly QRCodeGeneratorService _qrCodeService;
        private readonly ApplicationDbContext _dbContext;

        public InvoiceController(IConfiguration configuration, ILogger<InvoiceController> logger, QRCodeGeneratorService qrCodeService,
                             ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _qrCodeService = qrCodeService;
            _dbContext = dbContext;

        }

        [HttpPost("save-pdf")]
        public async Task<IActionResult> SaveInvoiceToServer([FromForm] IFormFile file)
        {
            try
            {
                string? pdfFolderPath = _configuration["FilePathConfig:GeneratedPdfFolder"];
                if (string.IsNullOrEmpty(pdfFolderPath))
                    return BadRequest(new { success = false, message = "PDF folder path not configured." });

                if (!Directory.Exists(pdfFolderPath))
                    Directory.CreateDirectory(pdfFolderPath);

                string fullPath = Path.Combine(pdfFolderPath, file.FileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream); // ✅ Use `CopyToAsync`
                }

                _logger.LogInformation("Invoice PDF saved to server: {fullPath}", fullPath);
                return Ok(new { success = true, message = "PDF saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PDF");
                return StatusCode(500, new { success = false, message = "An error occurred while saving the PDF." });
            }
        }


    }
}
