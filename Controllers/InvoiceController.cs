using System;
using System.IO;
using eInvWorld.Data;
using eInvWorld.Pages.Invoices;
using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Supplier")]
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

                if (file is null || file.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded." });

                // Never trust the client-supplied file name: strip any path component and enforce a .pdf extension.
                string safeName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(safeName) ||
                    !safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "Only .pdf files are allowed." });
                }

                if (!Directory.Exists(pdfFolderPath))
                    Directory.CreateDirectory(pdfFolderPath);

                string fullPath = Path.Combine(pdfFolderPath, safeName);

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
