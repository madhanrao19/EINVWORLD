using System.Security.Claims;
using EINVWORLD.Data;
using InputModels = EINVWORLD.Models.InputModel;
using JsonModels = EINVWORLD.Models.JsonModels;
using Documents = EINVWORLD.Models.JsonModels.Documents;
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
using EINVWORLD.Models.Logs;
using StatusCodes = EINVWORLD.Models.StatusCodes;
using EINVWORLD.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TimeZoneConverter; // Ensure you install the TimeZoneConverter package via NuGet

namespace EINVWORLD.Pages.Invoices
{
    public class CreateCNModel : PageModel
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


        public CreateCNModel(
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

        public string GeneratedJson { get; set; }
        public string Message { get; set; }
        public string SubmissionResult { get; private set; }
        public int? PrimaryCompanyId { get; set; } // Store Primary Supplier ID

        public void OnGet(string? invoiceNo = null, string? uuid = null)
        {
            try
            {
                int backdateSeconds = _configuration.GetValue<int>("InvoiceSettings:BackdateSeconds", 0);
                var currentUtcTime = DateTime.UtcNow.AddSeconds(-backdateSeconds);

                // Convert to Malaysia Time (Asia/Kuala_Lumpur)
                TimeZoneInfo malaysiaTimeZone = TZConvert.GetTimeZoneInfo("Asia/Kuala_Lumpur");

                InvoiceHeader? existingInvoice = null;

                // ✅ Fetch existing invoice based on `invoiceNo` or `UUID`
                if (!string.IsNullOrEmpty(invoiceNo))
                {
                    existingInvoice = _context.InvoiceHeaders
                        .Include(i => i.InvoiceLines)
                        .ThenInclude(il => il.InvoiceTaxes)
                        .FirstOrDefault(i => i.InvoiceNo == invoiceNo);
                }
                else if (!string.IsNullOrEmpty(uuid))
                {
                    existingInvoice = _context.InvoiceHeaders
                        .Include(i => i.InvoiceLines)
                        .ThenInclude(il => il.InvoiceTaxes)
                        .FirstOrDefault(i => i.UUID == uuid);
                }

                if (existingInvoice == null)
                {
                    _logger.LogWarning("Invoice not found for CN creation");
                    Message = "Invoice not found. Please enter a valid invoice number or UUID.";
                    Invoice = new InvoiceHeaderView
                    {
                        InvoiceNo = GenerateNextInvoiceNumber(),
                        IssueDate = currentUtcTime,
                        InvoiceLines = new List<InvoiceLineView> { new InvoiceLineView { Taxes = new List<InvoiceTaxView>() } }
                    };
                    return;
                }

               

                _logger.LogInformation("Populating Credit Note from InvoiceNo={InvoiceNo}, UUID={UUID}", existingInvoice.InvoiceNo, existingInvoice.UUID);

                int supplierId = existingInvoice.SupplierId ?? 0;
                int customerId = existingInvoice.CustomerId ?? 0;

                // ✅ Populate CN details from invoice
                Invoice = new InvoiceHeaderView
                {
                    InvoiceNo = GenerateNextInvoiceNumber(),
                    RefDocumentNo = existingInvoice.InvoiceNo ?? "",
                    UUID = "",
                    RefUUID = existingInvoice.UUID ?? "NA",  // ✅ Set RefUUID to the UUID of the original invoice

                    // ✅ Convert `IssueDate` to Malaysia Time (Keep Time)
                    IssueDate = existingInvoice.IssueDate.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(existingInvoice.IssueDate.Value, malaysiaTimeZone)
                        : (DateTime?)null,

                    DocTypeCode = "02", // Credit Note Doc Type
                    Currency = existingInvoice.Currency ?? "MYR",
                    ForeignCurrency = existingInvoice.ForeignCurrency ?? existingInvoice.Currency ?? "MYR", // Default to same currency
                    ExchangeRate = existingInvoice.ExchangeRate ?? 1.0m, // Set 1.0 if null

                    SupplierId = supplierId,
                    CustomerId = customerId,

                    TotalAmountExclTax = existingInvoice.TotalAmountExclTax,
                    TotalTaxAmount = existingInvoice.TotalTaxAmount,
                    TotalAmountIncTax = existingInvoice.TotalAmountIncTax,
                    TotalDiscountAmount = existingInvoice.TotalDiscountAmount,
                    TotalPayableAmount = existingInvoice.TotalPayableAmount,
                    TotalNetAmount = existingInvoice.TotalNetAmount,
                    InvoicePeriod = existingInvoice.InvoicePeriod,

                    // ✅ Convert `StartDate` & `EndDate` to Malaysia Time but Keep Only Date (Remove Time)
                    StartDate = existingInvoice.StartDate.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(existingInvoice.StartDate.Value, malaysiaTimeZone).Date
                        : (DateTime?)null,

                    EndDate = existingInvoice.EndDate.HasValue
                        ? TimeZoneInfo.ConvertTimeFromUtc(existingInvoice.EndDate.Value, malaysiaTimeZone).Date
                        : (DateTime?)null,

                    // ✅ Populate Invoice Lines for CN (Amounts are NEGATIVE)
                    InvoiceLines = existingInvoice.InvoiceLines.Select(line => new InvoiceLineView
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

                        // ✅ Populate Invoice Taxes (NEGATE Amounts)
                        Taxes = line.InvoiceTaxes.Select(tax => new InvoiceTaxView
                        {
                            TaxCategory = tax.TaxCategory,
                            TaxPercentage = tax.TaxPercentage,
                            TaxAmount = tax.TaxAmount
                        }).ToList() ?? new List<InvoiceTaxView>() // Prevent null reference
                    }).ToList() ?? new List<InvoiceLineView>()
                };

                _logger.LogInformation("Credit Note populated: {LineCount} lines, InvoiceNo={InvoiceNo}, RefUUID={RefUUID}",
                    Invoice.InvoiceLines.Count, Invoice.InvoiceNo, Invoice.RefUUID);


                // ✅ User Validation & Dropdown Initialization
                //LoadUserCompaniesAndDropdowns(supplierId);
                LoadUserCompaniesAndDropdowns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGet for Credit Note");
                Message = "An error occurred while loading the Credit Note. Please try again.";
            }
        }

        //private void LoadUserCompaniesAndDropdowns(int supplierId)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userCompanies = _context.UserCompanies
        //        .Where(uc => uc.UserId == userId)
        //        .Include(uc => uc.PartyInfo)
        //        .ToList();

        //    if (userCompanies.Any())
        //    {
        //        var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
        //        PrimarySupplierId = primaryCompany?.PartyInfoId;

        //        // **Ensure the user is the supplier of the invoice**
        //        var userIsSupplier = userCompanies.Any(uc => uc.PartyInfoId == supplierId);

        //        if (!userIsSupplier)
        //        {
        //            Console.WriteLine($"[WARNING] User is NOT the supplier for Invoice. Assigning Primary Company as Supplier.");
        //            supplierId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;
        //            Invoice.SupplierId = supplierId;
        //        }

        //        // ✅ **Ensure `Suppliers` list includes user’s companies**
        //        Suppliers = _context.PartyInfos
        //            .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId))
        //            .Select(p => new SelectListItem
        //            {
        //                Value = p.PartyInfoId.ToString(),
        //                Text = p.CompanyName
        //            })
        //            .ToList();

        //        if (!Suppliers.Any()) Suppliers = new List<SelectListItem>(); // Prevent null reference

        //        // ✅ **Ensure `Customers` list is initialized**
        //        Customers = _context.PartyInfos
        //            .Where(p => _context.SupplierBuyers.Any(sb => sb.SupplierId == supplierId && sb.BuyerId == p.PartyInfoId))
        //            .Select(p => new SelectListItem
        //            {
        //                Value = p.PartyInfoId.ToString(),
        //                Text = p.CompanyName
        //            })
        //            .ToList();

        //        if (!Customers.Any()) Customers = new List<SelectListItem>(); // Prevent null reference
        //    }
        //    else
        //    {
        //        Suppliers = new List<SelectListItem>();
        //        Customers = new List<SelectListItem>();
        //    }

        //    LoadDropdownData(); // Ensure dropdowns are populated
        //}

        //private void LoadUserCompaniesAndDropdowns(int supplierId)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var userCompanies = _context.UserCompanies
        //        .Where(uc => uc.UserId == userId)
        //        .Include(uc => uc.PartyInfo)
        //        .ToList();

        //    if (userCompanies.Any())
        //    {
        //        var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
        //        PrimarySupplierId = primaryCompany?.PartyInfoId;

        //        var assignedSuppliers = _context.SupplierBuyers
        //            .Where(sb => sb.BuyerId == primaryCompany.PartyInfoId)
        //            .Select(sb => sb.Supplier)
        //            .ToList();

        //        var uniqueSuppliers = new HashSet<int>();
        //        Suppliers = new List<SelectListItem>();

        //        foreach (var supplier in assignedSuppliers)
        //        {
        //            if (uniqueSuppliers.Add(supplier.PartyInfoId))
        //            {
        //                bool isPrimarySupplier = supplier.PartyInfoId == PrimarySupplierId;
        //                Suppliers.Add(new SelectListItem
        //                {
        //                    Value = supplier.PartyInfoId.ToString(),
        //                    Text = isPrimarySupplier ? $"⭐ {supplier.CompanyName} (Primary)" : supplier.CompanyName
        //                });
        //            }
        //        }

        //        Customers = userCompanies.Select(uc => new SelectListItem
        //        {
        //            Value = uc.PartyInfoId.ToString(),
        //            Text = uc.PartyInfo.CompanyName
        //        }).ToList();

        //        // ✅ **Load General TINs under Buyer Selection**
        //        var generalPublicTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000010");
        //        var foreignBuyerTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000020");
        //        var govBuyerTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000040");

        //        void AddGeneralTIN(PartyInfo tinInfo, string tinLabel)
        //        {
        //            if (tinInfo != null && !Customers.Any(c => c.Value == tinInfo.PartyInfoId.ToString()))
        //            {
        //                Customers.Add(new SelectListItem
        //                {
        //                    Value = tinInfo.PartyInfoId.ToString(),
        //                    Text = tinLabel
        //                });
        //            }
        //        }

        //        AddGeneralTIN(generalPublicTIN, "General Public's TIN");
        //        AddGeneralTIN(foreignBuyerTIN, "Foreign Buyer's TIN");
        //        AddGeneralTIN(govBuyerTIN, "Government Buyer's TIN");

        //        Invoice.SupplierId = Invoice.SupplierId > 0 ? Invoice.SupplierId : (Suppliers.Any() ? int.Parse(Suppliers.First().Value) : 0);
        //        Invoice.CustomerId = Invoice.CustomerId > 0 ? Invoice.CustomerId : (Customers.Any() ? int.Parse(Customers.First().Value) : 0);
        //    }
        //    else
        //    {
        //        Suppliers = new List<SelectListItem>();
        //        Customers = new List<SelectListItem>();
        //    }

        //    LoadDropdownData();
        //}

        private void LoadUserCompaniesAndDropdowns()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompanies = _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.PartyInfo)
                .ToList();

            if (userCompanies.Any())
            {
                var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
                PrimaryCompanyId = primaryCompany?.PartyInfoId;

                // ✅ Load Suppliers
                Suppliers = _context.PartyInfos
                    .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId)
                             || p.TIN == "EI00000000010"  // General Public's TIN
                             || p.TIN == "EI00000000030") // Foreign Supplier's TIN
                    .Select(p => new SelectListItem
                    {
                        Value = p.PartyInfoId.ToString(),
                        Text = p.CompanyName
                    })
                    .ToList();

                // ✅ Self-Billed: Swap supplier & buyer
                if (Invoice.DocTypeCode == "11" || Invoice.DocTypeCode == "12" ||
                    Invoice.DocTypeCode == "13" || Invoice.DocTypeCode == "14")
                {
                    _logger.LogInformation("Self-Billed CN detected. Switching Supplier and Buyer.");

                    // Buyer = User's primary company
                    Invoice.CustomerId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;

                    // Supplier = General TIN fallback
                    var generalTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN == "EI00000000010");
                    Invoice.SupplierId = (generalTIN?.PartyInfoId ?? primaryCompany?.PartyInfoId) ?? 0;
                }
                else
                {
                    // Normal CN
                    Invoice.SupplierId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;
                }

                // ✅ Load Customers
                Customers = _context.PartyInfos
                    .Where(p => _context.SupplierBuyers.Any(sb => sb.SupplierId == Invoice.SupplierId && sb.BuyerId == p.PartyInfoId)
                             || p.TIN == "EI00000000010" // General Public's TIN
                             || p.TIN == "EI00000000020" // Foreign Buyer's TIN
                             || p.TIN == "EI00000000040") // Government Buyer's TIN
                    .Select(p => new SelectListItem
                    {
                        Value = p.PartyInfoId.ToString(),
                        Text = p.CompanyName
                    })
                    .ToList();
            }
            else
            {
                Suppliers = new List<SelectListItem>();
                Customers = new List<SelectListItem>();
            }

            LoadDropdownData();
        }


        public JsonResult OnGetLoadCustomers(int supplierId)
        {
            var customers = _context.SupplierBuyers
                .Where(sb => sb.SupplierId == supplierId)
                .Include(sb => sb.Buyer)
                .Select(sb => new SelectListItem
                {
                    Value = sb.Buyer.PartyInfoId.ToString(),
                    Text = sb.Buyer.CompanyName
                })
                .ToList();

            return new JsonResult(customers);
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
                // ✅ Fetch the original invoice (the one being credited)
                var existingInvoice = _context.InvoiceHeaders
                    .FirstOrDefault(i => i.InvoiceNo == Invoice.RefDocumentNo);

                // ✅ Ensure RefUUID is linked properly
                if (string.IsNullOrEmpty(Invoice.RefUUID) && existingInvoice != null)
                {
                    Invoice.RefUUID = existingInvoice.UUID;
                }
                else if (string.IsNullOrEmpty(Invoice.RefUUID))
                {
                    Invoice.RefUUID = "NA"; // Default if no original invoice found
                }


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
                                _logger.LogInformation("Draft CN saved successfully: {DraftFilePath}", HttpContext.Session.GetString("DraftFilePath"));
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
                        _logger.LogInformation("Submitting documents for Credit Note: {InvoiceNo}", Invoice.InvoiceNo);
                        var draftFilePath = HttpContext.Session.GetString("DraftFilePath");

                        if (string.IsNullOrEmpty(draftFilePath))
                        {
                            ModelState.AddModelError(string.Empty, "Draft CN file path is missing. Save the draft first.");
                            return Page();
                        }

                        if (!System.IO.File.Exists(draftFilePath))
                        {
                            ModelState.AddModelError(string.Empty, "Draft CN file does not exist. Save the draft first.");
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

                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }

                // ✅ Ensure critical fields are set before saving the draft
                Invoice.DocTypeCode = !string.IsNullOrEmpty(Invoice.DocTypeCode) ? Invoice.DocTypeCode : "02";
                Invoice.Currency = !string.IsNullOrEmpty(Invoice.Currency) ? Invoice.Currency : "MYR";
                Invoice.ForeignCurrency = !string.IsNullOrEmpty(Invoice.ForeignCurrency) ? Invoice.ForeignCurrency : Invoice.Currency;

                _logger.LogDebug("SaveDraft - DocTypeCode={DocType}, Currency={Currency}, ForeignCurrency={ForeignCurrency}",
                    Invoice.DocTypeCode, Invoice.Currency, Invoice.ForeignCurrency);

                if (supplier == null)
                    throw new Exception("Supplier information is required.");

                if (customer == null)
                    throw new Exception("Customer information is required.");

                if (Invoice.InvoiceLines == null || !Invoice.InvoiceLines.Any())
                    throw new Exception("No invoice lines found. Cannot save an empty credit note.");

                // ✅ Save the draft in the database
                var draftInvoice = new InvoiceHeader
                {
                    InvoiceNo = Invoice.InvoiceNo,
                    PrefixedID = Invoice.InvoiceNo,
                    RefDocumentNo = Invoice.RefDocumentNo,
                    UUID = Invoice.UUID,
                    RefUUID = Invoice.RefUUID ?? "NA",
                    ForeignCurrency = Invoice.ForeignCurrency,
                    ExchangeRate = Invoice.ExchangeRate,
                    Supplier = supplier,
                    Customer = customer,
                    CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    IssueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"),
                    CreatedBy = User.Identity?.Name ?? "System",
                    UpdatedBy = User.Identity?.Name ?? "System",
                    LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    Currency = Invoice.Currency,
                    DocTypeCode = Invoice.DocTypeCode,
                    StartDate = Invoice.StartDate,
                    EndDate = Invoice.EndDate,
                    TotalAmountExclTax = Invoice.TotalAmountExclTax,
                    TotalTaxAmount = Invoice.TotalTaxAmount,
                    TotalAmountIncTax = Invoice.TotalAmountIncTax,
                    TotalPayableAmount = Invoice.TotalPayableAmount,
                    TotalNetAmount = Invoice.TotalNetAmount,
                    Notes = "Credit Note Draft",

                    // ✅ Save Invoice Lines
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

                _logger.LogDebug("Saving draft - DocTypeCode={DocType}, Lines={LineCount}", Invoice.DocTypeCode, Invoice.InvoiceLines.Count);

                _context.InvoiceHeaders.Add(draftInvoice);
                _context.SaveChanges();

                var draftsFolder = _filePathConfig.DraftFolder;
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
                _logger.LogInformation($"[Debug] Submitting Credit Note: {invoiceNo}");
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
                    ModelState.AddModelError(string.Empty, "Credit Note not found.");
                    return Page();
                }

                // Double-submit guard: a document that already has a MyInvois UUID was submitted —
                // resubmitting would create a duplicate e-invoice at LHDN.
                if (!string.IsNullOrWhiteSpace(existingInvoice.UUID))
                {
                    ModelState.AddModelError(string.Empty, $"Document {invoiceNo} has already been submitted to LHDN (UUID {existingInvoice.UUID}); it cannot be submitted again.");
                    return Page();
                }

                // Resolve the issuer TIN so submission uses the per-TIN token + onbehalfof header and an
                // ownership check (consistent with Create Invoice); falls back to the session token if null.
                submitterTin = EINVWORLD.Helpers.TinHelper.ResolveSubmitterTin(existingInvoice);
                if (!string.IsNullOrWhiteSpace(submitterTin)
                    && !await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(User, _context, submitterTin))
                {
                    ModelState.AddModelError(string.Empty, "You are not authorized to submit this document.");
                    return Page();
                }

                // ✅ Construct the draft file path
                var draftFilePath = Path.Combine(_filePathConfig.DraftFolder, $"{invoiceNo}.json");

                if (!System.IO.File.Exists(draftFilePath))
                {
                    ModelState.AddModelError(string.Empty, $"Draft file for CN {invoiceNo} does not exist.");
                    return Page();
                }

                // ✅ Load the JSON content
                var invoiceJson = await System.IO.File.ReadAllTextAsync(draftFilePath);

                // ✅ Encode and hash the document
                var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
                var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

                // ✅ Prepare the document for submission
                var documents = new List<Models.JsonModels.Documents>
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
                    ModelState.AddModelError(string.Empty, $"Document {invoiceNo} is already being submitted. Please wait a moment and try again.");
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
                        _logger.LogError($"[Error] Failed to fetch LHDN status for {invoiceNo}: {ex.Message}");
                    }
                }

                // ✅ Move JSON file to status folder based on LHDN status
                MoveJsonToStatusFolder(existingInvoice.InvoiceNo, existingInvoice.LHDNStatusId);

                return RedirectToPage("./InvoiceLists", new { refresh = true, timestamp = DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                await EINVWORLD.Helpers.InvoiceSubmissionGuard.ReleaseAsync(_context, invoiceNo);
                _logger.LogError($"[Error] Exception during submission of Invoice {invoiceNo}: {ex.Message}");

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
                    _logger.LogInformation($"[Success] Moved {invoiceNo}.json to {status} folder.");
                }
                else
                {
                    _logger.LogError($"[Error] File {invoiceNo}.json not found in Draft folder.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Error] Error moving file {invoiceNo}.json: {ex.Message}");
            }
        }


        private void UpdateInvoiceStatus(string invoiceNo, string lhdnStatus, string performedBy)
        {
            try
            {
                var submission = _context.InvoiceSubmissions.FirstOrDefault(i => i.InvoiceNo == invoiceNo);
                if (submission == null)
                {
                    _logger.LogWarning("Invoice submission {InvoiceNo} not found in database", invoiceNo);
                    return;
                }

                string internalStatus = _statusMappingService.MapLhdnStatusToInternalStatus(lhdnStatus);

                if (internalStatus == null)
                {
                    _logger.LogWarning("Unable to map LHDN status '{LhdnStatus}' to an internal status", lhdnStatus);
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
                _logger.LogInformation("Invoice submission {InvoiceNo} status updated to {Status}", invoiceNo, internalStatus);
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
                RefUUID = Invoice.RefUUID,
            };

            _logger.LogDebug("CreateInvoiceHeader: UUID={UUID}, RefUUID={RefUUID}", invoiceHeader.UUID, invoiceHeader.RefUUID);

            if (Invoice.DocTypeCode == "11" || Invoice.DocTypeCode == "12" ||
                Invoice.DocTypeCode == "13" || Invoice.DocTypeCode == "14")
            {
                _logger.LogInformation("Self-Billed Document detected. Swapping Supplier and Buyer.");

                invoiceHeader.Supplier = customer;
                invoiceHeader.Customer = supplier;

                var generalTIN = _context.PartyInfos.FirstOrDefault(p => p.TIN.StartsWith("EI0000000000"));
                if (generalTIN != null)
                {
                    invoiceHeader.Supplier = generalTIN;
                    _logger.LogInformation("General TIN assigned as Supplier: {TIN}", generalTIN.TIN);
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
