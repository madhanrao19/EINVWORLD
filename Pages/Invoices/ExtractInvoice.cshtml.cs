using eInvWorld.Data;
using eInvWorld.Models.JsonModels;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace EINVWORLD.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier")]
    public class OcrUploadModel : SupplierBasePage
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OcrUploadModel> _logger;

        public OcrUploadModel(IHttpClientFactory httpClientFactory, ApplicationDbContext context, ILogger<OcrUploadModel> logger) : base(context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [BindProperty]
        public IFormFile? UploadedPdf { get; set; }

        public InvoiceApiResponse? ExtractedData { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // 1. Retrieve the error message from TempData if it exists
            if (TempData.ContainsKey("ErrorMessage"))
            {
                ErrorMessage = TempData["ErrorMessage"]?.ToString();
            }

            // 2. Retrieve and deserialize the API response from TempData if it exists
            if (TempData.ContainsKey("ExtractedDataJson"))
            {
                var json = TempData["ExtractedDataJson"]?.ToString();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                if (json != null)
                    ExtractedData = JsonSerializer.Deserialize<InvoiceApiResponse>(json, options);
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (UploadedPdf == null || UploadedPdf.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a valid PDF file to upload.";
                return RedirectToPage(); // Redirect instead of returning Page()
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                using var formContent = new MultipartFormDataContent();
                using var fileStream = UploadedPdf.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);

                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(UploadedPdf.ContentType);
                formContent.Add(streamContent, "file", UploadedPdf.FileName);

                var response = await client.PostAsync("http://127.0.0.1:8000/extract-invoice", formContent);

                if (response.IsSuccessStatusCode)
                {
                    // Save the raw JSON string to TempData so it survives the redirect
                    var responseString = await response.Content.ReadAsStringAsync();
                    TempData["ExtractedDataJson"] = responseString;
                }
                else
                {
                    TempData["ErrorMessage"] = $"Extraction failed. API returned: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invoice extraction request to the OCR service failed.");
                TempData["ErrorMessage"] = "An error occurred while connecting to the extraction service.";
            }

            // Always redirect to the GET request at the end of a POST
            return RedirectToPage();
        }
    }
}