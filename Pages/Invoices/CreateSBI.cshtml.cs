using System.Security.Claims;
using EINVWORLD.Data;
using InputModels = EINVWORLD.Models.InputModel;
using JsonModels = EINVWORLD.Models.JsonModels;
using EINVWORLD.Models.ViewModels;
using EINVWORLD.Services.Mappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using EINVWORLD.Models;
using EINVWORLD.Models.InputModel;
using Microsoft.AspNetCore.Hosting;
using EINVWORLD.Services;
using EINVWORLD.Models.JsonModels;
using Microsoft.Extensions.Options;
using System.Configuration;
using EINVWORLD.Models.Document;
using Documents = EINVWORLD.Models.JsonModels.Documents;
using EINVWORLD.Models.Logs;
using StatusCodes = EINVWORLD.Models.StatusCodes;
using EINVWORLD.Services.Extensions;
using Microsoft.EntityFrameworkCore;


namespace EINVWORLD.Pages.Invoices
{
    public class CreateSBIModel : PageModel
    {
        private const string InvoicePrefix = "EINV"; // Prefix for invoice numbers
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;
        private readonly InvoiceMapper _invoiceMapper;
        private readonly InvoiceService _invoiceService;
        private readonly ILHDNApiService _lhdnApiService;
        private readonly ILogger<CreateModel> _logger;
        private readonly FilePathConfig _filePathConfig;
        private readonly IConfiguration _configuration;
        private readonly IStatusMappingService _statusMappingService;
        private readonly EINVWORLD.Services.Background.ISyncJobTracker _jobTracker;
        public CreateSBIModel(
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext context,
            InvoiceService invoiceService,
            ILHDNApiService lhdnApiService,
            ILogger<CreateModel> logger,
            IOptions<FilePathConfig> filePathConfig,
            IConfiguration configuration,
            IStatusMappingService statusMappingService,
            EINVWORLD.Services.Background.ISyncJobTracker jobTracker)
        {
            _webHostEnvironment = webHostEnvironment;
            _context = context;
            _invoiceMapper = new InvoiceMapper();
            _invoiceService = invoiceService;
            _lhdnApiService = lhdnApiService ?? throw new ArgumentNullException(nameof(lhdnApiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filePathConfig = filePathConfig.Value;
            _configuration = configuration;
            _statusMappingService = statusMappingService;
            _jobTracker = jobTracker;
        }

        [BindProperty]
        public InvoiceHeaderView Invoice { get; set; }
        public List<SelectListItem> CurrencyCodes { get; set; }
        public List<SelectListItem> InvoicePeriodEnum { get; set; }
        public List<SelectListItem> Suppliers { get; set; }
        public List<SelectListItem> Customers { get; set; }
        public List<EInvoiceType> EInvoiceTypes { get; set; }

        public List<SelectListItem> UserCompanies { get; set; }
        public List<SelectListItem> AvailableSuppliers { get; set; }
        public string AssignedSupplierId { get; set; }
        public string AssignedSupplierName { get; set; }
        public string GeneratedJson { get; set; }
        public string Message { get; set; }
        public string SubmissionResult { get; private set; }
        public int? PrimaryCompanyId { get; set; } // Store Primary Supplier ID

        public void OnGet()
        {
            int backdateSeconds = _configuration.GetValue<int>("InvoiceSettings:BackdateSeconds", 0);
            var currentUtcTime = DateTime.UtcNow.AddSeconds(-backdateSeconds);

            // Initialize Invoice if not loaded from draft
            if (Invoice == null)
            {
                Invoice = new InvoiceHeaderView
                {
                    InvoiceNo = PreviewNextInvoiceNumber(),
                    IssueDate = currentUtcTime,
                    InvoiceLines = new List<InvoiceLineView> { new InvoiceLineView { Taxes = new List<InvoiceTaxView>() } }
                };
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompanies = _context.UserCompanies
                                        .Where(uc => uc.UserId == userId)
                                        .Include(uc => uc.PartyInfo)
                                        .ToList();

            if (userCompanies.Any())
            {
                var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
                PrimaryCompanyId = primaryCompany?.PartyInfoId;

                // ✅ **Load General TINs**
                var generalPublicTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000010");
                //var foreignBuyerTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000020");
                var foreignSupplierTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000030");
                //var govSupplierTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000040");

                // ✅ **Get Assigned Suppliers**
                var assignedSuppliers = _context.SupplierBuyers
                    .Where(sb => sb.BuyerId == primaryCompany.PartyInfoId)
                    .Select(sb => sb.Supplier)
                    .ToList();

                // ✅ **Ensure Unique Suppliers**
                var uniqueSuppliers = new HashSet<int>();  // HashSet to track unique PartyInfoId

                Suppliers = new List<SelectListItem>();

                // ✅ Add assigned suppliers (ensuring uniqueness)
                foreach (var supplier in assignedSuppliers)
                {
                    if (uniqueSuppliers.Add(supplier.PartyInfoId))  // Ensures no duplicates
                    {
                        Suppliers.Add(new SelectListItem
                        {
                            Value = supplier.PartyInfoId.ToString(),
                            Text = supplier.CompanyName
                        });
                    }
                }

                // ✅ Function to add General TINs only if they are not already added
                void AddGeneralTIN(PartyInfo tinInfo, string tinLabel)
                {
                    if (tinInfo != null && uniqueSuppliers.Add(tinInfo.PartyInfoId))
                    {
                        Suppliers.Add(new SelectListItem
                        {
                            Value = tinInfo.PartyInfoId.ToString(),
                            Text = tinLabel
                        });
                    }
                }

                // ✅ Add General TINs
                AddGeneralTIN(generalPublicTIN, "General Public's TIN");
                //AddGeneralTIN(foreignBuyerTIN, "Foreign Buyer's TIN");
                AddGeneralTIN(foreignSupplierTIN, "Foreign Supplier's TIN");
                //AddGeneralTIN(govSupplierTIN, "Government Supplier's TIN");

                // ✅ **Buyer Selection - All User Assigned Companies**
                Customers = userCompanies.Select(uc => new SelectListItem
                {
                    Value = uc.PartyInfoId.ToString(),
                    Text = uc.IsPrimaryCompany ? "⭐ " + uc.PartyInfo.CompanyName + " (Primary)" : uc.PartyInfo.CompanyName
                }).ToList();

                // ✅ **Auto-Select Primary Supplier & Buyer**
                Invoice.SupplierId = Invoice.SupplierId > 0 ? Invoice.SupplierId : (Suppliers.Any() ? int.Parse(Suppliers.First().Value) : 0);
                Invoice.CustomerId = Invoice.CustomerId > 0 ? Invoice.CustomerId : (Customers.Any() ? int.Parse(Customers.First().Value) : 0);
            }
            else
            {
                Suppliers = new List<SelectListItem>();
                Customers = new List<SelectListItem>();
            }

            LoadDropdownData();
        }


        private void LoadDropdownData()
        {
            EInvoiceTypes = _context.EInvoiceTypes.Where(e => e.IsActive).ToList();
            CurrencyCodes = _context.CurrencyCodes
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem { Text = $"{c.Currency} ({c.Code})", Value = c.Code })
                .ToList();
            InvoicePeriodEnum = Enum.GetValues(typeof(InputModels.InvoicePeriodEnum))
                .Cast<InputModels.InvoicePeriodEnum>()
                .Select(e => new SelectListItem { Text = e.ToString(), Value = e.ToString() })
                .ToList();

        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            try
            {

                // Generate Invoice Number if missing
                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }


                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed. Lines={LineCount}", Invoice.InvoiceLines.Count);
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogWarning("Validation error - Key={Key}: {Error}", state.Key, error.ErrorMessage);
                        }
                    }

                    Message = "Validation failed. Please correct the errors and try again.";
                    OnGet();
                    return Page();
                }

                foreach (var line in Invoice.InvoiceLines)
                {
                    line.CalculateAmounts();
                }

                var supplier = _context.PartyInfos.FirstOrDefault(p => p.PartyInfoId == Invoice.SupplierId);
                var customer = _context.PartyInfos.FirstOrDefault(p => p.PartyInfoId == Invoice.CustomerId);

                if (supplier == null || customer == null)
                {
                    Message = supplier == null ? "Supplier information is missing." : "Customer information is missing.";
                    _logger.LogWarning("Validation failed: {Message}", Message);
                    OnGet();
                    return Page();
                }

                // Generate Invoice Number only when required
                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }
                var invoiceHeader = CreateInvoiceHeader(supplier, customer);
                var invoiceJson = _invoiceMapper.MapToJsonModel(invoiceHeader);

                // ✅ Ensure tax amounts are recalculated before saving
                foreach (var line in invoiceHeader.InvoiceLines)
                {
                    line.CalculateAmounts();
                }

                // Handle actions
                // Existing logic for saveDraft and submitDocuments actions

                switch (action)
                {
                    case "generateJson":
                        _logger.LogInformation("Generating JSON for Invoice: {InvoiceNo}", Invoice.InvoiceNo);
                        GeneratedJson = invoiceJson;
                        Message = "JSON generated successfully!";
                        break;

                    case "saveDraft":
                        try
                        {
                            if (SaveDraft(invoiceJson, supplier, customer))
                            {
                                _logger.LogInformation("Draft saved successfully: {DraftFilePath}", HttpContext.Session.GetString("DraftFilePath"));
                                // Return success, draft path, and invoice number
                                return new JsonResult(new
                                {
                                    success = true,
                                    draftPath = HttpContext.Session.GetString("DraftFilePath"),
                                    invoiceNo = Invoice.InvoiceNo  // Include the Invoice Number in the response
                                });
                            }
                            else
                            {
                                _logger.LogError("Failed to save draft.");
                                return new JsonResult(new { success = false, message = "Failed to save draft." });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Exception occurred while saving draft: {Exception}", ex);
                            return new JsonResult(new { success = false, message = "An error occurred while saving the draft." });
                        }

                    case "submitDocuments":
                        _logger.LogInformation("Submitting documents for Invoice: {InvoiceNo}", Invoice.InvoiceNo);
                        var draftFilePath = HttpContext.Session.GetString("DraftFilePath");

                        if (string.IsNullOrEmpty(draftFilePath))
                        {
                            ModelState.AddModelError(string.Empty, "Draft file path is missing. Save the draft first.");
                            return Page();
                        }

                        if (!System.IO.File.Exists(draftFilePath))
                        {
                            ModelState.AddModelError(string.Empty, "Draft file does not exist. Save the draft first.");
                            return Page();
                        }

                        LoadDraft(draftFilePath); // Load the draft to populate the model
                        if (!ModelState.IsValid)
                        {
                            Message = "Validation failed. Please correct the errors and try again.";
                            return Page();
                        }
                        string invoiceNo = Invoice?.InvoiceNo ?? throw new InvalidOperationException("Invoice number is not set.");
                        return await OnPostSubmitDocumentsAsync(invoiceNo);

                    default:
                        _logger.LogWarning("Unknown action: {Action}", action);
                        ModelState.AddModelError(string.Empty, "Unknown action.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in OnPostAsync");
                Message = "An error occurred while processing your request. Please try again.";
            }

            OnGet();
            return Page();
        }


        private bool SaveDraft(string invoiceJson, PartyInfo supplier, PartyInfo customer)
        {
            try
            {
                // Step 1: Generate a unique invoice number
                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }

                // Step 1: Save the draft in the database
                var draftInvoice = new InvoiceHeader
                {
                    InvoiceNo = Invoice.InvoiceNo,
                    PrefixedID = Invoice.InvoiceNo, // Example for prefixed ID if needed
                    RefDocumentNo = Invoice.RefDocumentNo,
                    UUID = Invoice.UUID,
                    ForeignCurrency = Invoice.Currency ?? "MYR",
                    ExchangeRate = Invoice.ExchangeRate,
                    Supplier = supplier,
                    Customer = customer,
                    IssueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")), // Use current date for the draft
                    InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"), // Set status to 'Draft'
                    CreatedBy = User.Identity.Name ?? "System", // Record the user or system
                    UpdatedBy = User.Identity.Name ?? "System", // Default value for UpdatedBy
                    LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")), // Set current time as the last update
                    Currency = Invoice.Currency ?? "MYR", // Default currency if none provided
                    DocTypeCode = Invoice.DocTypeCode ?? "01", // Default document type code if none provided
                    StartDate = Invoice.StartDate,
                    EndDate = Invoice.EndDate,
                    TotalAmountExclTax = Invoice.TotalAmountExclTax,
                    TotalTaxAmount = Invoice.TotalTaxAmount,
                    TotalAmountIncTax = Invoice.TotalAmountIncTax,
                    TotalPayableAmount = Invoice.TotalPayableAmount,
                    TotalNetAmount = Invoice.TotalNetAmount,
                    Notes = "Invoice JSON generated", // Default value for Notes
                                                      // Populate other fields as necessary for drafts

                    // ✅ Save invoice lines
                    InvoiceLines = Invoice.InvoiceLines?.Select(line => new InputModels.InvoiceLine
                    {
                        LineNumber = line.LineNumber,
                        ItemCode = line.ItemCode,
                        ItemDescription = line.ItemDescription,
                        UnitOfMeasure = line.UnitOfMeasure,
                        UnitPrice = line.UnitPrice,
                        Quantity = line.Quantity,
                        AmountInclTax = line.AmountInclTax,
                        AmountExclTax = line.AmountExclTax,
                        DiscountAmount = line.DiscountAmount,
                        ClassificationCode = line.ClassificationCode,
                        InvoiceTaxes = line.Taxes?.Select(tax => new InvoiceTax
                        {
                            TaxCategory = tax.TaxCategory,
                            TaxPercentage = tax.TaxPercentage,
                            TaxAmount = tax.TaxAmount
                        }).ToList()
                    }).ToList()
                };


                _context.InvoiceHeaders.Add(draftInvoice);
                _context.SaveChanges();

                // Step 3: Save the JSON file to the drafts folder on another server
                var draftsFolder = _filePathConfig.DraftFolder;  // Directly from config (supports network path)

                _logger.LogInformation("Drafts folder path: {DraftsFolder}", draftsFolder);

                if (!Directory.Exists(draftsFolder))
                {
                    _logger.LogInformation("Drafts folder does not exist. Creating it...");
                    Directory.CreateDirectory(draftsFolder);
                }

                var fileName = $"{Invoice.InvoiceNo}.json";
                var filePath = Path.Combine(draftsFolder, fileName);

                _logger.LogInformation("Saving draft to: {FilePath}", filePath);
                System.IO.File.WriteAllText(filePath, invoiceJson);

                HttpContext.Session.SetString("DraftFilePath", filePath);
                ViewData["DraftFilePath"] = filePath;

                _logger.LogInformation("Draft saved successfully at: {FilePath}", filePath);

                Message = $"Draft saved as {fileName}";

                // Clear the DraftFilePath from the session to avoid reloading the same draft
                HttpContext.Session.Remove("DraftFilePath");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save draft");
                return false;
            }
        }

        // Consolidated into InvoiceService (numeric max + defensive parse, fixes >EINV99999 + crash on "EINV..(1)").
        private string PreviewNextInvoiceNumber() => _invoiceService.PreviewNextInvoiceNumber();

        private string GenerateNextInvoiceNumber() => _invoiceService.GenerateNextInvoiceNumber();


        private void LoadDraft(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException("Draft file not found.");
                }

                var invoiceJson = System.IO.File.ReadAllText(filePath);
                Invoice = JsonConvert.DeserializeObject<InvoiceHeaderView>(invoiceJson);

                // Repopulate dropdown data after loading draft
                LoadDropdownData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading draft");
                Message = "Failed to load draft.";
            }
        }

        public async Task<IActionResult> OnPostSubmitDocumentsAsync(string invoiceNo)
        {
            // Declared at method scope (not inside the try) so the catch block below can still report
            // which TIN a failed submission was for when queuing a background retry job.
            string? submitterTin = null;
            try
            {
                _logger.LogInformation("Submitting Invoice: {InvoiceNo}", invoiceNo);
                if (string.IsNullOrEmpty(invoiceNo))
                {
                    ModelState.AddModelError(string.Empty, "Invoice number is missing.");
                    return Page();
                }

                // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
                if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, invoiceNo))
                {
                    _logger.LogWarning("SubmitDocuments denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, invoiceNo);
                    ModelState.AddModelError(string.Empty, "You are not authorized to submit this invoice.");
                    return Page();
                }

                // ✅ Fetch invoice from database
                var existingInvoice = _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer)
                    .FirstOrDefault(i => i.InvoiceNo == invoiceNo);
                if (existingInvoice == null)
                {
                    ModelState.AddModelError(string.Empty, "Invoice not found.");
                    return Page();
                }

                // Double-submit guard: an invoice that already has a MyInvois UUID was submitted —
                // resubmitting would create a duplicate e-invoice at LHDN.
                if (!string.IsNullOrWhiteSpace(existingInvoice.UUID))
                {
                    ModelState.AddModelError(string.Empty, $"Invoice {invoiceNo} has already been submitted to LHDN (UUID {existingInvoice.UUID}); it cannot be submitted again.");
                    return Page();
                }

                // Resolve the issuer TIN so submission uses the per-TIN token + onbehalfof header and an
                // ownership check (consistent with Create Invoice); falls back to the session token if null.
                submitterTin = EINVWORLD.Helpers.TinHelper.ResolveSubmitterTin(existingInvoice);
                if (!string.IsNullOrWhiteSpace(submitterTin)
                    && !await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(User, _context, submitterTin))
                {
                    ModelState.AddModelError(string.Empty, "You are not authorized to submit this invoice.");
                    return Page();
                }

                // ✅ Construct the draft file path
                var draftFilePath = Path.Combine(_filePathConfig.DraftFolder, $"{invoiceNo}.json");

                if (!System.IO.File.Exists(draftFilePath))
                {
                    ModelState.AddModelError(string.Empty, $"Draft file for Invoice {invoiceNo} does not exist.");
                    return Page();
                }

                // ✅ Load the JSON content
                var invoiceJson = await System.IO.File.ReadAllTextAsync(draftFilePath);

                // ✅ Encode and hash the document
                var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
                var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

                // ✅ Prepare the document for submission
                var documents = new List<Documents>
                {
                    new Documents("JSON", documentHash, invoiceNo, encodedDocument)
                };

                // ✅ Get access token from session
                var accessToken = HttpContext.Session.GetString("AccessToken");
                if (string.IsNullOrEmpty(accessToken))
                {
                    ModelState.AddModelError(string.Empty, "Access token is missing or expired. Please log in again.");
                    return Page();
                }

                // Atomic double-submit guard: only one concurrent request wins this claim; others are
                // blocked here so the document can't be posted to LHDN twice.
                if (!await EINVWORLD.Helpers.InvoiceSubmissionGuard.TryClaimAsync(_context, invoiceNo))
                {
                    _logger.LogWarning("[Guard] Concurrent submit blocked for {InvoiceNo}.", invoiceNo);
                    ModelState.AddModelError(string.Empty, $"Invoice {invoiceNo} is already being submitted. Please wait a moment and try again.");
                    return Page();
                }

                // ✅ Submit the invoice to LHDN API
                var apiResponseJson = await _lhdnApiService.SubmitDocumentsAsync(documents, submitterTin);
                var apiResponse = JsonConvert.DeserializeObject<SuccessSubmit>(apiResponseJson);

                if (apiResponse.acceptedDocuments.Any())
                {
                    var acceptedDoc = apiResponse.acceptedDocuments.First();

                    existingInvoice.UUID = acceptedDoc.uuid;
                    existingInvoice.SubmissionID = apiResponse.submissionUID;
                    existingInvoice.LHDNStatusId = "Submitted";
                    existingInvoice.InternalStatusId = _statusMappingService.GetStatusIdByCode("Submitted");
                    existingInvoice.LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                    existingInvoice.UpdatedBy = User.Identity.Name ?? "System";

                    _context.InvoiceHeaders.Update(existingInvoice);
                    await _context.SaveChangesAsync();
                }

                // ✅ Fetch LHDN Status after submission (only if UUID is present)
                if (!string.IsNullOrEmpty(existingInvoice.UUID))
                {
                    try
                    {
                        var documentStatus = await _lhdnApiService.GetDocumentDetailsAsync(existingInvoice.UUID, accessToken);
                        if (documentStatus != null)
                        {
                            existingInvoice.LHDNStatusId = documentStatus.status;
                            existingInvoice.InternalStatusId = _statusMappingService.MapLhdnStatusToInternalStatus(documentStatus.status);
                            existingInvoice.LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

                            _context.InvoiceHeaders.Update(existingInvoice);
                            await _context.SaveChangesAsync();
                        }
                        _logger.LogInformation("Updated LHDN Status for {InvoiceNo}: {Status}", invoiceNo, documentStatus?.status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to fetch LHDN status for {InvoiceNo}", invoiceNo);
                    }
                }

                // ✅ Move JSON file to status folder based on LHDN status
                MoveJsonToStatusFolder(existingInvoice.InvoiceNo, existingInvoice.LHDNStatusId);

                return RedirectToPage("./InvoiceLists", new { refresh = true, timestamp = DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                await EINVWORLD.Helpers.InvoiceSubmissionGuard.ReleaseAsync(_context, invoiceNo);
                _logger.LogError(ex, "Exception during submission of Invoice {InvoiceNo}", invoiceNo);

                // Queue a background retry so a transient failure (LHDN outage/network blip) doesn't
                // require the user to notice and resubmit — it auto-retries, and lands in
                // Admin -> Sync Jobs (Failed) for visibility/manual replay if every attempt fails.
                await _jobTracker.CreateAsync(
                    submitterTin ?? string.Empty, EINVWORLD.Services.Background.SyncJobType.SubmitDocument,
                    User.Identity?.Name ?? "System",
                    EINVWORLD.Services.Background.SyncJobPayload.CreateForInvoice(invoiceNo));

                SubmissionResult = $"Error submitting Invoice {invoiceNo}. A retry has been queued automatically.";
                return Page();
            }
        }


        private void MoveJsonToStatusFolder(string invoiceNo, string status)
        {
            try
            {
                var currentFilePath = Path.Combine(_filePathConfig.DraftFolder, $"{invoiceNo}.json");

                // Determine correct target folder based on LHDN status
                var targetFolder = status switch
                {
                    "Valid" => _filePathConfig.ValidFolder,
                    "Invalid" => _filePathConfig.InvalidFolder,
                    "Submitted" => _filePathConfig.SubmittedFolder,
                    "Cancelled" => _filePathConfig.CancelledFolder,
                    _ => _filePathConfig.DraftFolder
                };

                var newFilePath = Path.Combine(targetFolder, $"{invoiceNo}.json");

                // Ensure target folder exists
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // Move the file only if it exists
                if (System.IO.File.Exists(currentFilePath))
                {
                    System.IO.File.Move(currentFilePath, newFilePath, true);
                    _logger.LogInformation("Moved {InvoiceNo}.json to {Status} folder.", invoiceNo, status);
                }
                else
                {
                    _logger.LogWarning("File {InvoiceNo}.json not found in Draft folder.", invoiceNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file {InvoiceNo}.json", invoiceNo);
            }
        }


        private void UpdateInvoiceStatus(string invoiceNo, string lhdnStatus, string performedBy)
        {
            try
            {
                var submission = _context.InvoiceSubmissions.FirstOrDefault(i => i.InvoiceNo == invoiceNo);
                if (submission == null)
                {
                    _logger.LogWarning("Invoice submission {InvoiceNo} not found in the database.", invoiceNo);
                    return;
                }

                // Map LHDN status to internal status using your mapping service
                string internalStatus = _statusMappingService.MapLhdnStatusToInternalStatus(lhdnStatus);

                if (internalStatus == null)
                {
                    _logger.LogWarning("Unable to map LHDN status '{LhdnStatus}' to an internal status.", lhdnStatus);
                    return;
                }

                // Update the submission status
                submission.InternalStatusId = internalStatus;
                submission.LHDNStatusId = lhdnStatus;
                submission.LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                submission.UpdatedBy = performedBy;

                // Log the activity
                var activityLog = new ActivityLog
                {
                    InvoiceNo = invoiceNo,
                    Action = lhdnStatus == "Invalid" ? "Resubmit Required" : "Status Updated",
                    Status = internalStatus,
                    ActionDate = DateTime.UtcNow,
                    PerformedBy = performedBy,
                    Notes = lhdnStatus
                };
                _context.ActivityLogs.Add(activityLog);

                _context.SaveChanges();
                _logger.LogInformation("Invoice submission {InvoiceNo} status updated to {InternalStatus}.", invoiceNo, internalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for Invoice {InvoiceNo}", invoiceNo);
            }
        }

        private string ReadLastInvoiceNumber(string filePath)
        {
            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Check if the file exists; if not, create it with an initial value
            if (!System.IO.File.Exists(filePath))
            {
                // Create the counter file with an initial value
                string initialInvoice = $"{InvoicePrefix}00000";
                System.IO.File.WriteAllText(filePath, initialInvoice);
                return initialInvoice;
            }

            // Read the last invoice number from the file
            var lines = System.IO.File.ReadAllLines(filePath);
            var lastInvoice = lines.LastOrDefault()?.Trim() ?? $"{InvoicePrefix}00000";
            return lastInvoice.StartsWith(InvoicePrefix) ? lastInvoice : $"{InvoicePrefix}00000";
        }

        private InputModels.InvoiceHeader CreateInvoiceHeader(PartyInfo supplier, PartyInfo customer)
        {
            var invoiceHeader = new InputModels.InvoiceHeader
            {
                InvoiceNo = Invoice.InvoiceNo,
                RefDocumentNo = Invoice.RefDocumentNo,
                IssueDate = Invoice.IssueDate,
                DocTypeCode = Invoice.DocTypeCode,
                InvoicePeriod = Invoice.InvoicePeriod,
                Currency = Invoice.Currency,
                ForeignCurrency = Invoice.ForeignCurrency,
                ExchangeRate = Invoice.ExchangeRate,
                UUID = Invoice.UUID,
            };

            _logger.LogDebug("Mapped UUID in CreateInvoiceHeader: {UUID}", invoiceHeader.UUID);

            // ✅ Detect Self-Billed Invoice Type (11, 12, 13, 14)
            if (Invoice.DocTypeCode == "11" || Invoice.DocTypeCode == "12" ||
                Invoice.DocTypeCode == "13" || Invoice.DocTypeCode == "14")
            {
                _logger.LogDebug("Self-Billed Document detected. Swapping Supplier and Buyer.");

                // Swap Supplier and Buyer
                invoiceHeader.Supplier = customer;
                invoiceHeader.Customer = supplier;

                // ✅ Assign General TIN as Supplier if applicable
                var generalTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN.StartsWith("EI0000000000"));
                if (generalTIN != null)
                {
                    invoiceHeader.Supplier = generalTIN;
                    _logger.LogDebug("General TIN assigned as Supplier: {TIN}", generalTIN.TIN);
                }
            }
            else
            {
                // ✅ Normal Invoice Processing (Keep Supplier & Buyer as is)
                invoiceHeader.Supplier = supplier;
                invoiceHeader.Customer = customer;
            }

            // Populate invoice amounts
            invoiceHeader.TotalDiscountAmount = Invoice.TotalDiscountAmount;
            invoiceHeader.StartDate = Invoice.StartDate;
            invoiceHeader.EndDate = Invoice.EndDate;
            invoiceHeader.TotalAmountExclTax = Invoice.TotalAmountExclTax;
            invoiceHeader.TotalTaxAmount = Invoice.TotalTaxAmount;
            invoiceHeader.TotalAmountIncTax = Invoice.TotalAmountIncTax;
            invoiceHeader.TotalPayableAmount = Invoice.TotalPayableAmount;
            invoiceHeader.TotalNetAmount = Invoice.TotalNetAmount;

            // ✅ Populate Invoice Lines
            invoiceHeader.InvoiceLines = Invoice.InvoiceLines.Select(line => new InputModels.InvoiceLine
            {
                LineNumber = line.LineNumber,
                Quantity = line.Quantity,
                ItemCode = line.ItemCode,
                ItemDescription = line.ItemDescription,
                UnitOfMeasure = line.UnitOfMeasure,
                UnitPrice = line.UnitPrice,
                Subtotal = (line.Quantity ?? 0) * (line.UnitPrice ?? 0),
                AmountInclTax = line.AmountInclTax,
                AmountExclTax = line.AmountExclTax,
                DiscountAmount = line.DiscountAmount,
                ClassificationCode = line.ClassificationCode,
                InvoiceTaxes = line.Taxes.Select(tax => new InputModels.InvoiceTax
                {
                    TaxCategory = tax.TaxCategory,
                    TaxPercentage = tax.TaxPercentage,
                    TaxAmount = tax.TaxAmount
                }).ToList()
            }).ToList();

            // Assign InvoiceHeader to each InvoiceLine
            foreach (var line in invoiceHeader.InvoiceLines)
            {
                line.InvoiceHeader = invoiceHeader;
                line.CalculateAmounts();
            }

            return invoiceHeader;

        }

        private string GetInternalStatus(string invoiceId)
        {
            var submission = _context.InvoiceSubmissions.FirstOrDefault(s => s.InvoiceNo == invoiceId);
            return submission?.InternalStatus?.StatusCode ?? "Unknown";
        }
    }

}
