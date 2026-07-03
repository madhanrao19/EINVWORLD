using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using eInvWorld.Models.Logs;
using eInvWorld.Models.Templates;
using eInvWorld.Models.ViewModels;
using eInvWorld.Services;
using eInvWorld.Services.Extensions;
using eInvWorld.Services.Logging;
using eInvWorld.Services.Mappers;
using EINVWORLD.Helpers;
using EINVWORLD.Pages.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Documents = eInvWorld.Models.JsonModels.Documents;
using InputModels = eInvWorld.Models.InputModel;
using JsonModels = eInvWorld.Models.JsonModels;
using StatusCodes = eInvWorld.Models.StatusCodes;

namespace eInvWorld.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier")]
    public class InvoiceEditModel : SupplierBasePage
    {
        private const string InvoicePrefix = "EINV"; // Prefix for invoice numbers
        private readonly IWebHostEnvironment _webHostEnvironment;
        private new readonly ApplicationDbContext _context;
        private readonly InvoiceMapper _invoiceMapper;
        private readonly InvoiceService _invoiceService;
        private readonly ILHDNApiService _lhdnApiService;
        private readonly ILogger<CreateInvoiceModel> _logger;
        private readonly FilePathConfig _filePathConfig;
        private readonly IConfiguration _configuration;
        private readonly IStatusMappingService _statusMappingService;
        private readonly InvoiceHistoryService _invoiceHistoryService;
        private readonly IJsonFileService _jsonFileService;
        private readonly DropdownHelper _dropdownHelper;
        private readonly InvoiceTemplateService _invoiceTemplateService;
        private readonly ITokenService _tokenService;
        private readonly IBuyerService _buyerService;
        private readonly EINVWORLD.Services.Background.ISyncJobTracker _jobTracker;
        public InvoiceEditModel(
           IWebHostEnvironment webHostEnvironment,
           ApplicationDbContext context,
           InvoiceService invoiceService,
           ILHDNApiService lhdnApiService,
           ILogger<CreateInvoiceModel> logger,
           IOptions<FilePathConfig> filePathConfig,
           IConfiguration configuration,
           IStatusMappingService statusMappingService,
           InvoiceHistoryService invoiceHistoryService,
           IJsonFileService jsonFileService,
           DropdownHelper dropdownHelper,
           InvoiceTemplateService invoiceTemplateService,
           ITokenService tokenService,
           IBuyerService buyerService,
           EINVWORLD.Services.Background.ISyncJobTracker jobTracker) : base(context)
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
            _invoiceHistoryService = invoiceHistoryService;
            _jsonFileService = jsonFileService;
            _dropdownHelper = dropdownHelper;
            _invoiceTemplateService = invoiceTemplateService;
            _tokenService = tokenService;
            _buyerService = buyerService;
            _jobTracker = jobTracker;
        }

        public List<SelectListItem> SavedItems { get; set; } = new List<SelectListItem>();

        // Override model validation for template mode
        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Always check template mode first (most reliable indicator)
            if (IsTemplateMode || Request.Query.ContainsKey("templateId"))
            {
                _logger.LogDebug("OnPageHandlerExecuting: Template mode detected, clearing all validation");
                ModelState.Clear();

                var problematicKeys = new[] { "id", "Id", "Invoice.Id", "InvoiceId" };
                foreach (var key in problematicKeys)
                {
                    if (ModelState.ContainsKey(key))
                        ModelState.Remove(key);
                }
            }

            if (Request.Method == "POST" && Request.HasFormContentType)
            {
                try
                {
                    var action = context.HandlerArguments.ContainsKey("action") ?
                        context.HandlerArguments["action"]?.ToString() : "";

                    if (string.IsNullOrEmpty(action) && Request.Form.ContainsKey("action"))
                        action = Request.Form["action"].ToString();

                    var isTemplateOperation = action == "updateTemplate" ||
                                            action == "saveAsTemplate" ||
                                            Request.Form.ContainsKey("TemplateName") ||
                                            !string.IsNullOrEmpty(Request.Form["TemplateName"]);

                    if (isTemplateOperation)
                    {
                        _logger.LogDebug("OnPageHandlerExecuting: Template operation detected: {Action}", action);
                        ModelState.Clear();

                        var allKeys = ModelState.Keys.ToList();
                        foreach (var key in allKeys)
                            ModelState.Remove(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnPageHandlerExecuting");
                }
            }

            base.OnPageHandlerExecuting(context);
        }

        [BindProperty]
        public InvoiceHeaderView Invoice { get; set; } = null!;
        [BindProperty]
        public string? SelectedBuyerId { get; set; }
        public List<SelectListItem> CurrencyCodes { get; set; } = new();
        public List<SelectListItem> InvoicePeriodOptions { get; set; } = new();
        public List<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Customers { get; set; } = new List<SelectListItem>();
        public List<EInvoiceType> EInvoiceTypes { get; set; } = new List<EInvoiceType>();
        public List<SelectListItem> DocTypeSelectList { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ClassificationCodes { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> UnitOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> TaxCategoryOptions { get; set; } = new List<SelectListItem>();

        public string? GeneratedJson { get; set; }
        public string? Message { get; set; }
        public string? SubmissionResult { get; private set; }
        public int? PrimaryCompanyId { get; set; } // Store Primary Supplier ID
        public List<object> BankDetails { get; set; } = new List<object>(); // Store bank details

        [BindProperty]
        public string? TemplateName { get; set; }
        public List<SelectListItem> Templates { get; set; } = new();
        public bool IsTemplateMode { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(string? id, int? templateId = null)
        {
            // Handle template editing mode (when templateId is provided but no invoice id)
            if (string.IsNullOrEmpty(id) && templateId.HasValue)
            {
                return await LoadTemplateForEditingAsync(templateId.Value);
            }

            if (string.IsNullOrEmpty(id))
            {
                return NotFound("Invoice ID is required for editing.");
            }

            try
            {
                _logger.LogInformation("Loading invoice for editing: {InvoiceId}", id);

                // Load existing invoice with related data
                var existingInvoice = await _context.InvoiceHeaders
                    .Include(h => h.Supplier)
                    .Include(h => h.Customer)
                    .Include(h => h.PublicCustomer) // ✅ Include PublicCustomer
                    .Include(h => h.InvoiceLines)
                        .ThenInclude(l => l.InvoiceTaxes)
                    .FirstOrDefaultAsync(h => h.InvoiceNo == id);

                if (existingInvoice == null)
                {
                    return NotFound($"Invoice {id} not found.");
                }

                // Map existing invoice to view model
                Invoice = MapToInvoiceHeaderView(existingInvoice);

                // ✅ Pre-select correct Buyer ID (Hybrid Logic)
                if (Invoice.CustomerId > 0)
                {
                    SelectedBuyerId = $"PI_{Invoice.CustomerId}";
                }
                else if (Invoice.PublicCustomerId > 0)
                {
                    SelectedBuyerId = $"PC_{Invoice.PublicCustomerId}";
                }

                // Load dropdown data immediately after mapping (Async version)
                await LoadDropdownDataAsync();

                // Load suppliers and customers for dropdown population
                await LoadSuppliersAndCustomers();

                // Load bank details
                BankDetails = await LoadBankDetailsAsync();

                // Load all dropdown options for edit mode
                ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
                UnitOptions = _dropdownHelper.GetUnitOptions();
                TaxCategoryOptions = _dropdownHelper.GetTaxCategoryOptions();

                _logger.LogInformation("Successfully loaded invoice {InvoiceId} for editing", id);

                return Page(); // Return early for edit mode
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice for editing: {InvoiceId}", id);
                return BadRequest($"Error loading invoice: {ex.Message}");
            }

        }


        private async Task LoadSuppliersAndCustomers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userCompanies = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.PartyInfo)
                .ToListAsync();

            if (userCompanies.Any())
            {
                var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
                PrimaryCompanyId = primaryCompany?.PartyInfoId;

                // ----------------------------------------------------
                // Suppliers (unchanged logic)
                // ----------------------------------------------------
                Suppliers = await _context.PartyInfos
                    .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId)
                             || p.TIN == "EI00000000010"
                             || p.TIN == "EI00000000030")
                    .Select(p => new SelectListItem
                    {
                        Value = p.PartyInfoId.ToString(),
                        Text = p.CompanyName
                    })
                    .ToListAsync();

                // ----------------------------------------------------
                // 🔥 CRITICAL FIX: Use SAME buyer service as CreateInvoice
                // ----------------------------------------------------
                if (Invoice?.SupplierId > 0)
                {
                    Customers = await _buyerService
                        .GetCombinedBuyersBySupplierAsync(Invoice.SupplierId);
                }
                else
                {
                    Customers = new List<SelectListItem>();
                }
            }
            else
            {
                Suppliers = new List<SelectListItem>();
                Customers = new List<SelectListItem>();
            }
        }


        private InvoiceHeaderView MapToInvoiceHeaderView(InvoiceHeader existingInvoice)
        {
            return new InvoiceHeaderView
            {
                InvoiceNo = existingInvoice.InvoiceNo,
                RefDocumentNo = existingInvoice.RefDocumentNo,
                DocTypeCode = existingInvoice.DocTypeCode,
                IssueDate = existingInvoice.IssueDate,
                Currency = existingInvoice.Currency,
                ForeignCurrency = existingInvoice.ForeignCurrency ?? "MYR",
                ExchangeRate = existingInvoice.ExchangeRate,
                SupplierId = existingInvoice.Supplier?.PartyInfoId ?? 0,
                CustomerId = existingInvoice.Customer?.PartyInfoId ?? 0,
                PublicCustomerId = existingInvoice.PublicCustomer?.PublicCustomerId ?? 0,
                UUID = existingInvoice.UUID,
                PoDoNo = existingInvoice.PoDoNo,
                OriginalInvoiceDate = existingInvoice.OriginalInvoiceDate,
                Attention = existingInvoice.Attention,
                BankName = existingInvoice.BankName,
                BankAccountNo = existingInvoice.BankAccountNo,
                PaymentTerms = existingInvoice.PaymentTerms,
                StartDate = existingInvoice.StartDate,
                EndDate = existingInvoice.EndDate,
                InvoicePeriod = existingInvoice.InvoicePeriod,
                TotalAmountExclTax = existingInvoice.TotalAmountExclTax,
                TotalTaxAmount = existingInvoice.TotalTaxAmount,
                TotalAmountIncTax = existingInvoice.TotalAmountIncTax,
                TotalPayableAmount = existingInvoice.TotalPayableAmount,
                TotalNetAmount = existingInvoice.TotalNetAmount,
                TotalDiscountAmount = existingInvoice.TotalDiscountAmount,
                InvoiceLines = existingInvoice.InvoiceLines?.Select(line => new InvoiceLineView
                {
                    LineNumber = line.LineNumber,
                    ItemCode = line.ItemCode,
                    ItemDescription = line.ItemDescription,
                    ClassificationCode = line.ClassificationCode,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    Subtotal = line.Subtotal,
                    AmountExclTax = line.AmountExclTax,
                    AmountInclTax = line.AmountInclTax,
                    Taxes = line.InvoiceTaxes?.Select(tax => new InvoiceTaxView
                    {
                        TaxCategory = tax.TaxCategory,
                        TaxPercentage = tax.TaxPercentage,
                        TaxAmount = tax.TaxAmount,
                        TaxExemptionReason = tax.TaxExemptionReason
                    }).ToList() ?? new List<InvoiceTaxView>()
                }).ToList() ?? new List<InvoiceLineView>()
            };
        }


        private async Task LoadDropdownDataAsync()
        {
            EInvoiceTypes = await _context.EInvoiceTypes
                .Where(e => e.IsActive)
                .ToListAsync();

            var currentDocTypeCode = Invoice?.DocTypeCode;

            DocTypeSelectList = await _context.EInvoiceTypes
                .Where(e => e.IsActive)
                .Select(e => new SelectListItem
                {
                    Text = e.Description,
                    Value = e.Code,
                    Selected = e.Code == currentDocTypeCode
                })
                .ToListAsync();

            CurrencyCodes = await _context.CurrencyCodes
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem
                {
                    Text = $"{c.Currency} ({c.Code})",
                    Value = c.Code
                })
                .ToListAsync();

            InvoicePeriodOptions = Enum.GetValues(typeof(InputModels.InvoicePeriodEnum))
                .Cast<InvoicePeriodEnum>()
                .Select(e => new SelectListItem
                {
                    Text = e.ToString(),
                    Value = e.ToString()
                })
                .ToList();

            UnitOptions = _dropdownHelper.GetUnitOptions();
            TaxCategoryOptions = _dropdownHelper.GetTaxCategoryOptions();
            ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();

            // --- Populate Suppliers and Customers ---
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userCompanies = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.PartyInfo)
                .ToListAsync();

            if (userCompanies.Any())
            {
                var primaryCompany = userCompanies.FirstOrDefault(uc => uc.IsPrimaryCompany);
                PrimaryCompanyId = primaryCompany?.PartyInfoId;

                // Include user companies and General TINs in supplier options
                Suppliers = await _context.PartyInfos
                    .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId)
                             || p.TIN == "EI00000000010"
                             || p.TIN == "EI00000000030")
                    .Select(p => new SelectListItem
                    {
                        Value = p.PartyInfoId.ToString(),
                        Text = p.CompanyName
                    })
                    .ToListAsync();

                // Preserve existing self-billed logic
                if (IsSelfBilled(Invoice?.DocTypeCode ?? ""))
                {
                    Invoice!.CustomerId = primaryCompany?.PartyInfoId
                                         ?? userCompanies.First().PartyInfoId;

                    var generalTIN = await _context.PartyInfos
                        .FirstOrDefaultAsync(p => p.TIN == "EI00000000010");

                    Invoice!.SupplierId = generalTIN?.PartyInfoId
                                         ?? (primaryCompany?.PartyInfoId
                                         ?? userCompanies.First().PartyInfoId);
                }
                else
                {
                    Invoice!.SupplierId = primaryCompany?.PartyInfoId
                                         ?? userCompanies.First().PartyInfoId;
                }

                // ✅ Updated Customer Loading Logic (Hybrid via BuyerService)
                if (Invoice.SupplierId > 0)
                {
                    Customers = await _buyerService
                        .GetCombinedBuyersBySupplierAsync(Invoice.SupplierId);
                }
                else
                {
                    Customers = new List<SelectListItem>();
                }

                // ✅ ADDED SAVED ITEMS LOGIC HERE
                if (PrimaryCompanyId.HasValue)
                {
                    var savedItems = await _context.ItemDescriptions
                        .Where(i => i.CreatedByCompanyId == PrimaryCompanyId.Value && i.IsActive)
                        .Select(i => new
                        {
                            i.ItemCode,
                            i.Description,
                            i.ClassificationCode
                        })
                        .ToListAsync();

                    SavedItems = savedItems.Select(i =>
                    {
                        // Safely handle null descriptions
                        var desc = i.Description ?? "";

                        // Limit to 50 characters, add "..." if it's longer
                        var shortDesc = desc.Length > 50 ? desc.Substring(0, 47) + "..." : desc;

                        return new SelectListItem
                        {
                            // Store the FULL description in the JSON value so auto-fill still works perfectly
                            Value = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                ItemCode = i.ItemCode,
                                Description = desc,
                                ClassificationCode = i.ClassificationCode
                            }),
                            // Display the SHORTENED description in the dropdown UI
                            Text = $"{i.ItemCode} - {shortDesc}"
                        };
                    }).ToList();
                }
                else
                {
                    SavedItems = new List<SelectListItem>();
                }
            }
            else
            {
                Suppliers = new List<SelectListItem>();
                Customers = new List<SelectListItem>();
                SavedItems = new List<SelectListItem>();
            }
        }


        // Method to load bank details for suppliers
        private async Task<List<object>> LoadBankDetailsAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompanies = _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.PartyInfo)
                .ToList();

            if (userCompanies.Any())
            {
                var bankDetails = await _context.PartyInfos
                    .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId)
                             || p.TIN == "EI00000000010" || p.TIN == "EI00000000030")
                    .Select(p => new
                    {
                        PartyInfoId = p.PartyInfoId,
                        CompanyName = p.CompanyName,
                        BankName = p.BankName,
                        BankAccountNo = p.BankAccountNo
                    })
                    .ToListAsync();

                return bankDetails.Cast<object>().ToList();
            }

            return new List<object>();
        }

        public async Task<IActionResult> OnPostAsync(string? action, [FromRoute] string? id = null)
        {
            var isAjax = Request.Headers["X-Requested-With"].ToString().Contains("XMLHttpRequest");

            try
            {
                if (string.IsNullOrWhiteSpace(action))
                    action = Request.Form["invoiceAction"].ToString();

                if (Invoice != null && !string.IsNullOrEmpty(SelectedBuyerId))
                {
                    var parts = SelectedBuyerId.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        if (parts[0] == "PI")
                        {
                            Invoice.CustomerId = parsedId;
                            Invoice.PublicCustomerId = null;
                        }
                        else if (parts[0] == "PC")
                        {
                            Invoice.PublicCustomerId = parsedId;
                            Invoice.CustomerId = null;
                        }
                    }
                }

                // PRIORITY 1: Template Save
                if (action == "saveAsTemplate")
                {
                    var templateName = Request.Form["TemplateName"].ToString();
                    return await SaveAsTemplateAsync(templateName);
                }

                // PRIORITY 2: Template Update
                var isTemplateUpdate = action == "updateTemplate" ||
                                       IsTemplateMode ||
                                       (!string.IsNullOrEmpty(Request.Form["TemplateName"]) &&
                                        Request.Form["TemplateName"] != "");

                if (isTemplateUpdate)
                {
                    ModelState.Clear();
                    return await UpdateTemplateAsync();
                }

                if (Invoice == null)
                    ModelState.AddModelError("", "Invoice payload is missing.");

                // Defaults BEFORE validation
                if (Invoice != null)
                {
                    if (string.IsNullOrWhiteSpace(Invoice.InvoiceNo))
                        Invoice.InvoiceNo = id ?? GenerateNextInvoiceNumber();

                    if (string.IsNullOrWhiteSpace(Invoice.ForeignCurrency))
                        Invoice.ForeignCurrency = Invoice.Currency ?? "MYR";
                }

                if (Invoice?.InvoiceLines == null || !Invoice.InvoiceLines.Any())
                    ModelState.AddModelError("", "At least one invoice item is required.");

                if (Invoice?.InvoiceLines != null &&
                    Invoice.InvoiceLines.Any(l => l.Taxes == null || !l.Taxes.Any()))
                    ModelState.AddModelError("", "Each invoice item must have at least one tax.");

                // ===============================
                // Supplier / Customer Resolution
                // ===============================

                PartyInfo? supplier = null;
                PartyInfo? customer = null;
                eInvWorld.Models.InputModel.PublicCustomer? publicCustomer = null; // ✅ New

                if (Invoice != null)
                {
                    supplier = await _context.PartyInfos
                        .FirstOrDefaultAsync(p => p.PartyInfoId == Invoice.SupplierId);

                    // ✅ Parse SelectedBuyerId (Hybrid Buyer Logic)
                    if (!string.IsNullOrEmpty(SelectedBuyerId))
                    {
                        var parts = SelectedBuyerId.Split('_');

                        if (parts.Length == 2 && int.TryParse(parts[1], out int parsedId))
                        {
                            if (parts[0] == "PI")
                            {
                                customer = await _context.PartyInfos.FindAsync(parsedId);
                                Invoice.CustomerId = parsedId;
                                Invoice.PublicCustomerId = null; // Ensure exclusivity
                            }
                            else if (parts[0] == "PC")
                            {
                                publicCustomer = await _context.PublicCustomers.FindAsync(parsedId);
                                Invoice.PublicCustomerId = parsedId;
                                Invoice.CustomerId = null; // Ensure exclusivity
                            }
                        }
                    }

                    if (supplier == null)
                        ModelState.AddModelError("", "Supplier information is missing.");

                    if (customer == null && publicCustomer == null)
                        ModelState.AddModelError("SelectedBuyerId", "Please select a valid buyer.");
                }

                // ===============================
                // Classification + Tax + Totals
                // ===============================

                if (Invoice != null && supplier != null && Invoice.InvoiceLines != null)
                {
                    var isSelfBilled = IsSelfBilled(Invoice.DocTypeCode ?? "");
                    var supplierTin = supplier.TIN ?? "";

                    var defaultClassificationCode = "022";
                    if (isSelfBilled)
                        defaultClassificationCode = supplierTin == "EI00000000030" ? "035" : "004";

                    foreach (var line in Invoice.InvoiceLines)
                    {
                        if (string.IsNullOrWhiteSpace(line.ClassificationCode))
                            line.ClassificationCode = defaultClassificationCode;

                        if (isSelfBilled)
                        {
                            var generalPublicValidCodes = new[] { "004" };
                            var foreignSupplierValidCodes = new[]
                            {
                        "010","011","033","034","035","036",
                        "037","038","039","040","041","045"
                    };

                            if (supplierTin == "EI00000000010")
                            {
                                if (!generalPublicValidCodes.Contains(line.ClassificationCode))
                                    ModelState.AddModelError("",
                                        "For self-billed with supplier: General Public, classification code must be 004.");
                            }
                            else if (supplierTin == "EI00000000030")
                            {
                                if (!foreignSupplierValidCodes.Contains(line.ClassificationCode))
                                    ModelState.AddModelError("",
                                        "For self-billed with supplier: Foreign Supplier, classification code must be one of: 010, 011, 033, 034, 035, 036, 037, 038, 039, 040, 041, 045.");
                            }
                        }

                        if (line.Taxes != null)
                        {
                            foreach (var tax in line.Taxes)
                            {
                                if (tax.TaxCategory == "E" &&
                                    string.IsNullOrWhiteSpace(tax.TaxExemptionReason))
                                {
                                    tax.TaxExemptionReason =
                                        "Tax exempted as per applicable regulations";
                                }
                            }
                        }

                        line.CalculateAmounts();
                    }

                    Invoice.CalculateInvoiceTotals();
                }

                // ===============================
                // Validation Exit
                // ===============================

                if (!ModelState.IsValid)
                {
                    var allErrors = ModelState
                        .SelectMany(kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>())
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();

                    if (isAjax)
                        return AjaxFail(allErrors.FirstOrDefault() ?? "Validation failed.", 400, allErrors);

                    Message = "Validation failed. Please correct the errors and try again.";
                    await OnGetAsync(Invoice?.InvoiceNo ?? id ?? "");
                    return Page();
                }

                // ===============================
                // Build JSON Model
                // ===============================

                var invoiceHeader = CreateInvoiceHeader(
                    supplier!,
                    customer,
                    publicCustomer); // ✅ Pass both types

                var invoiceJson = _invoiceMapper.MapToJsonModel(invoiceHeader);

                foreach (var line in invoiceHeader.InvoiceLines)
                    line.CalculateAmounts();

                // ===============================
                // Action Switch
                // ===============================

                switch (action)
                {
                    case "generateJson":
                        GeneratedJson = invoiceJson;
                        Message = "JSON generated successfully!";

                        if (isAjax)
                            return new JsonResult(new
                            {
                                success = true,
                                message = Message,
                                invoiceNo = Invoice!.InvoiceNo
                            });

                        return Page();

                    case "saveDraft":
                        if (SaveDraft(invoiceJson, supplier!, customer, publicCustomer)) // ✅ Updated
                        {
                            var wasUpdate = _context.InvoiceHeaders
                                .Any(ih => ih.InvoiceNo == Invoice!.InvoiceNo);

                            return new JsonResult(new
                            {
                                success = true,
                                draftPath = HttpContext.Session.GetString("DraftFilePath"),
                                invoiceNo = Invoice!.InvoiceNo,
                                isUpdate = wasUpdate
                            });
                        }

                        return isAjax ? AjaxFail("Failed to save draft.", 500) : Page();

                    case "submitDocuments":
                        return await OnPostSubmitDocumentsAsync(Invoice!.InvoiceNo ?? string.Empty, true);

                    case "saveAndSubmit":
                        if (!SaveDraft(invoiceJson, supplier!, customer, publicCustomer))
                            return AjaxFail("Could not save draft. Submission aborted.", 500);

                        return await OnPostSubmitDocumentsAsync(Invoice!.InvoiceNo ?? string.Empty, true);

                    case "updateTemplate":
                        return await UpdateTemplateAsync();

                    case "saveAsTemplate":
                        return await SaveAsTemplateAsync(TemplateName ?? string.Empty);

                    default:
                        return isAjax ? AjaxFail("Unknown action.", 400) : Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in OnPostAsync");

                if (isAjax)
                    return AjaxFail(
                        "An error occurred while processing your request. Please try again.",
                        500);

                Message = "An error occurred while processing your request. Please try again.";
                await OnGetAsync(Invoice?.InvoiceNo ?? id ?? "");
                return Page();
            }
        }



        // ✅ Updated signature
        private bool SaveDraft(string invoiceJson, PartyInfo supplier, PartyInfo? customer, eInvWorld.Models.InputModel.PublicCustomer? publicCustomer)
        {
            try
            {
                // Step 1: Generate a unique invoice number
                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }

                foreach (var line in Invoice.InvoiceLines)
                    line.CalculateAmounts();

                Invoice.CalculateInvoiceTotals();

                var existingDraft = _context.InvoiceHeaders
                    .Include(ih => ih.InvoiceLines)
                    .FirstOrDefault(ih => ih.InvoiceNo == Invoice.InvoiceNo);

                bool isUpdate = existingDraft != null;
                _logger.LogDebug("SaveDraft - Invoice {InvoiceNo}: {Action}", Invoice.InvoiceNo, isUpdate ? "UPDATING existing draft" : "CREATING new draft");

                InvoiceHeader draftInvoice;

                if (isUpdate)
                {
                    draftInvoice = existingDraft!;

                    _context.InvoiceLines.RemoveRange(draftInvoice.InvoiceLines);

                    draftInvoice.RefDocumentNo = Invoice.RefDocumentNo;
                    draftInvoice.UUID = Invoice.UUID;
                    draftInvoice.ForeignCurrency = Invoice.Currency ?? "MYR";
                    draftInvoice.ExchangeRate = Invoice.ExchangeRate;
                    draftInvoice.Supplier = supplier;
                    draftInvoice.UpdatedBy = User.Identity?.Name ?? "System";
                    draftInvoice.LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.UtcNow,
                        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

                    draftInvoice.IssueDate = Invoice.IssueDate;
                    draftInvoice.Currency = Invoice.Currency ?? "MYR";
                    draftInvoice.DocTypeCode = Invoice.DocTypeCode ?? "01";
                    draftInvoice.StartDate = Invoice.StartDate;
                    draftInvoice.EndDate = Invoice.EndDate;
                    draftInvoice.TotalAmountExclTax = Invoice.TotalAmountExclTax;
                    draftInvoice.TotalTaxAmount = Invoice.TotalTaxAmount;
                    draftInvoice.TotalAmountIncTax = Invoice.TotalAmountIncTax;
                    draftInvoice.TotalPayableAmount = Invoice.TotalPayableAmount;
                    draftInvoice.TotalNetAmount = Invoice.TotalNetAmount;
                    draftInvoice.OriginalInvoiceDate = Invoice.OriginalInvoiceDate;
                    draftInvoice.PoDoNo = Invoice.PoDoNo;
                    draftInvoice.BankAccountNo = Invoice.BankAccountNo;
                    draftInvoice.BankName = Invoice.BankName;
                    draftInvoice.Attention = Invoice.Attention;
                    draftInvoice.PaymentTerms = Invoice.PaymentTerms ?? "";

                    // ✅ Correct buyer relationship handling
                    if (customer != null)
                    {
                        draftInvoice.Customer = customer;
                        draftInvoice.CustomerId = customer.PartyInfoId;
                        draftInvoice.PublicCustomer = null;
                        draftInvoice.PublicCustomerId = null;
                    }
                    else if (publicCustomer != null)
                    {
                        draftInvoice.Customer = null!;
                        draftInvoice.CustomerId = null;
                        draftInvoice.PublicCustomer = publicCustomer;
                        draftInvoice.PublicCustomerId = publicCustomer.PublicCustomerId;
                    }
                }
                else
                {
                    draftInvoice = new InvoiceHeader
                    {
                        InvoiceNo = Invoice.InvoiceNo,
                        PrefixedID = Invoice.InvoiceNo,
                        RefDocumentNo = Invoice.RefDocumentNo,
                        UUID = Invoice.UUID,
                        ForeignCurrency = Invoice.Currency ?? "MYR",
                        ExchangeRate = Invoice.ExchangeRate,
                        Supplier = supplier!,
                        Customer = customer!,
                        PublicCustomer = publicCustomer,     // ✅ added
                        CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.UtcNow,
                            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                        IssueDate = Invoice.IssueDate,
                        InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"),
                        CreatedBy = User.Identity?.Name ?? "System",
                        UpdatedBy = User.Identity?.Name ?? "System",
                        LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.UtcNow,
                            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                        Currency = Invoice.Currency ?? "MYR",
                        DocTypeCode = Invoice.DocTypeCode ?? "01",
                        StartDate = Invoice.StartDate,
                        EndDate = Invoice.EndDate,
                        TotalAmountExclTax = Invoice.TotalAmountExclTax,
                        TotalTaxAmount = Invoice.TotalTaxAmount,
                        TotalAmountIncTax = Invoice.TotalAmountIncTax,
                        TotalPayableAmount = Invoice.TotalPayableAmount,
                        TotalNetAmount = Invoice.TotalNetAmount,
                        Notes = "Invoice JSON generated",
                        OriginalInvoiceDate = Invoice.OriginalInvoiceDate,
                        PoDoNo = Invoice.PoDoNo,
                        BankAccountNo = Invoice.BankAccountNo,
                        BankName = Invoice.BankName,
                        Attention = Invoice.Attention,
                        PaymentTerms = Invoice.PaymentTerms ?? ""
                    };
                }

                // Step 3: Add invoice lines
                var newLines = Invoice.InvoiceLines?.Select(lineView =>
                {
                    var line = new InputModels.InvoiceLine
                    {
                        LineNumber = lineView.LineNumber,
                        Quantity = lineView.Quantity,
                        ItemCode = lineView.ItemCode ?? "",
                        ItemDescription = lineView.ItemDescription,
                        UnitOfMeasure = lineView.UnitOfMeasure,
                        UnitPrice = lineView.UnitPrice,
                        DiscountAmount = lineView.DiscountAmount,
                        ClassificationCode = lineView.ClassificationCode,
                        InvoiceHeader = draftInvoice,
                        InvoiceTaxes = (lineView.Taxes?.Select(tax => new InvoiceTax
                        {
                            TaxCategory = tax.TaxCategory,
                            TaxPercentage = tax.TaxPercentage,
                            TaxAmount = tax.TaxAmount,
                            TaxExemptionReason = tax.TaxCategory == "E" &&
                                                 string.IsNullOrWhiteSpace(tax.TaxExemptionReason)
                                ? "Tax exempted as per applicable regulations"
                                : tax.TaxExemptionReason ?? ""
                        }).ToList()) ?? new List<InvoiceTax>()
                    };

                    line.CalculateAmounts();
                    return line;
                }).ToList();

                if (isUpdate)
                {
                    draftInvoice.InvoiceLines = newLines ?? new List<InputModels.InvoiceLine>();
                }
                else
                {
                    draftInvoice.InvoiceLines = newLines ?? new List<InputModels.InvoiceLine>();
                    _context.InvoiceHeaders.Add(draftInvoice);
                }

                _context.SaveChanges();

                _invoiceHistoryService.Log(
                    draftInvoice.InvoiceNo,
                    isUpdate ? "Updated" : "Created",
                    isUpdate ? "Invoice draft updated" : "Invoice draft created");

                // File save logic (unchanged)
                var draftsFolder = _filePathConfig.DraftFolder;

                if (!Directory.Exists(draftsFolder))
                    Directory.CreateDirectory(draftsFolder);

                var fileName = $"{Invoice.InvoiceNo}.json";
                var filePath = Path.Combine(draftsFolder, fileName);

                System.IO.File.WriteAllText(filePath, invoiceJson);

                HttpContext.Session.SetString("DraftFilePath", filePath);
                ViewData["DraftFilePath"] = filePath;

                Message = $"Draft saved as {fileName}";

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save draft");
                return false;
            }
        }

        // Consolidated into InvoiceService (numeric max + defensive parse; fixes the >EINV99999 string-sort bug).
        private string GenerateNextInvoiceNumber() => _invoiceService.GenerateNextInvoiceNumber();

        private async Task LoadDraft(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException("Draft file not found.");
                }

                var invoiceJson = System.IO.File.ReadAllText(filePath);
                Invoice = JsonConvert.DeserializeObject<InvoiceHeaderView>(invoiceJson) ?? new InvoiceHeaderView();

                // Repopulate dropdown data after loading draft
                await LoadDropdownDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading draft from {FilePath}", filePath);
                Message = "Failed to load draft.";
            }
        }

        public async Task<IActionResult> OnPostSubmitDocumentsAsync([FromForm] string invoiceNo, [FromForm] bool isAjax)
        {
            // Declared at method scope (not inside the try) so the catch block below can still report
            // which TIN a failed submission was for when queuing a background retry job.
            string? tin = null;
            try
            {
                _logger.LogInformation($"[Debug] Submitting Invoice: {invoiceNo}, Ajax: {isAjax}");

                if (string.IsNullOrEmpty(invoiceNo))
                {
                    return new JsonResult(new { success = false, message = "Invoice number is missing." });
                }

                // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
                if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, invoiceNo))
                {
                    _logger.LogWarning("SubmitDocuments denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, invoiceNo);
                    return new JsonResult(new { success = false, message = "You are not authorized to submit this invoice." });
                }

                var existingInvoice = _context.InvoiceHeaders.FirstOrDefault(i => i.InvoiceNo == invoiceNo);
                if (existingInvoice == null)
                {
                    var msg = $"Invoice {invoiceNo} not found.";
                    _logger.LogWarning(msg);
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                // Double-submit guard: an invoice that already has a MyInvois UUID was submitted —
                // resubmitting would create a duplicate e-invoice at LHDN.
                if (!string.IsNullOrWhiteSpace(existingInvoice.UUID))
                {
                    var msg = $"Invoice {invoiceNo} has already been submitted to LHDN (UUID {existingInvoice.UUID}); it cannot be submitted again.";
                    _logger.LogWarning("[Guard] Double-submit blocked for {InvoiceNo} (UUID {UUID}).", invoiceNo, existingInvoice.UUID);
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                var issueDate = existingInvoice.IssueDate;
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
                if ((now - issueDate)?.TotalDays > 3)
                {
                    var msg = $"The invoice issue date ({issueDate:dd-MMM-yyyy}) is more than 3 days old. Please create a new invoice.";
                    _logger.LogWarning("[Validation] Invoice too old to submit: " + msg);
                    return isAjax
                        ? new JsonResult(new { success = false, message = msg })
                        : Page();
                }

                var jsonPath = _jsonFileService.GetExistingFilePath(invoiceNo);
                if (string.IsNullOrEmpty(jsonPath) || !System.IO.File.Exists(jsonPath))
                {
                    var msg = $"Draft file for Invoice {invoiceNo} does not exist.";
                    _logger.LogWarning(msg);
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                var invoiceJson = await System.IO.File.ReadAllTextAsync(jsonPath);
                var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
                var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

                var documents = new List<Documents>
                {
                    new Documents("JSON", documentHash, invoiceNo, encodedDocument)
                };

                tin = await _tokenService.GetUserAssignedTINAsync(); // Secure source

                var accessToken = await _tokenService.GetAccessTokenForTIN(tin);

                // Atomic double-submit guard: exactly one concurrent request wins this claim; any other
                // in-flight request for the same invoice is blocked so the document can't be posted twice.
                if (!await EINVWORLD.Helpers.InvoiceSubmissionGuard.TryClaimAsync(_context, invoiceNo))
                {
                    _logger.LogWarning("[Guard] Concurrent submit blocked for {InvoiceNo}.", invoiceNo);
                    var busyMsg = $"Invoice {invoiceNo} is already being submitted. Please wait a moment and refresh.";
                    return isAjax ? new JsonResult(new { success = false, message = busyMsg }) : Page();
                }

                // Pass the resolved TIN so submission uses the per-TIN token and adds the onbehalfof
                // header, instead of relying on session state (which is empty right after a 2FA login).
                var apiResponseJson = await _lhdnApiService.SubmitDocumentsAsync(documents, tin);
                _logger.LogInformation("[Debug] LHDN API raw response: " + apiResponseJson);

                var apiResponse = JsonConvert.DeserializeObject<SuccessSubmit>(apiResponseJson);

                string? uuid = null;
                string submissionUid = apiResponse?.submissionUID ?? "";
                string invoiceCodeNumber = invoiceNo;

                if (apiResponse?.acceptedDocuments?.Any() == true)
                {
                    var acceptedDoc = apiResponse.acceptedDocuments.First();
                    uuid = acceptedDoc.uuid;
                    invoiceCodeNumber = acceptedDoc.invoiceCodeNumber;

                    existingInvoice.UUID = uuid;
                    existingInvoice.SubmissionID = submissionUid;
                    existingInvoice.LHDNStatusId = "Submitted";
                    existingInvoice.InternalStatusId = _statusMappingService.GetStatusIdByCode("Submitted");
                    existingInvoice.LastUpdated = now;
                    existingInvoice.UpdatedBy = User.Identity?.Name ?? "System";

                    _context.InvoiceHeaders.Update(existingInvoice);
                    await _context.SaveChangesAsync();

                    _invoiceHistoryService.Log(invoiceNo, "Submitted", $"Submitted to LHDN - SubmissionUID: {submissionUid}");
                }

                var finalStatus = await _lhdnApiService.PollSubmissionStatusAsync(submissionUid, accessToken);
                if (finalStatus != null)
                {
                    existingInvoice.LHDNStatusId = finalStatus.status;
                    existingInvoice.InternalStatusId = _statusMappingService.MapLhdnStatusToInternalStatus(finalStatus.status) ?? existingInvoice.InternalStatusId;
                    existingInvoice.LastUpdated = now;

                    _context.InvoiceHeaders.Update(existingInvoice);
                    await _context.SaveChangesAsync();

                    _invoiceHistoryService.Log(invoiceNo, "StatusSync", $"Fetched status: {finalStatus.status}");
                }

                _jsonFileService.MoveToStatusFolder(existingInvoice.InvoiceNo ?? "", existingInvoice.LHDNStatusId ?? "");

                if (isAjax)
                {
                    if (!string.IsNullOrEmpty(uuid))
                    {
                        var dbInvoice = await _context.InvoiceHeaders
                            .AsNoTracking()
                            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

                        _logger.LogInformation($"✅ Invoice {invoiceNo} submitted successfully. UUID: {uuid}");

                        return new JsonResult(new
                        {
                            success = true,
                            uuid = dbInvoice?.UUID,
                            submissionUid = dbInvoice?.SubmissionID,
                            invoiceCodeNumber = dbInvoice?.InvoiceNo
                        });
                    }
                    else
                    {
                        _logger.LogError($"❌ Submission accepted but UUID was missing for Invoice {invoiceNo}.");
                        return new JsonResult(new
                        {
                            success = false,
                            message = $"Submission accepted but UUID is missing for Invoice {invoiceNo}."
                        });
                    }
                }
                else
                {
                    _logger.LogInformation($"✅ Invoice {invoiceNo} submitted successfully (non-AJAX). UUID: {uuid ?? "N/A"}");

                    return RedirectToPage("./InvoiceLists", new
                    {
                        refresh = true,
                        invoiceDirection = "Sent",
                        timestamp = DateTime.UtcNow.Ticks
                    });
                }
            }
            catch (Exception ex)
            {
                // Release the claim so the user can retry; LHDN's DS302 duplicate detection backstops the
                // rare "accepted then errored" case.
                await EINVWORLD.Helpers.InvoiceSubmissionGuard.ReleaseAsync(_context, invoiceNo);
                _logger.LogError(ex, $"[Error] Exception during submission of Invoice: {invoiceNo}");

                // Queue a background retry so a transient failure (LHDN outage/network blip) doesn't
                // require the user to notice and resubmit — it auto-retries, and lands in
                // Admin -> Sync Jobs (Failed) for visibility/manual replay if every attempt fails.
                await _jobTracker.CreateAsync(
                    tin ?? string.Empty, eInvWorld.Models.Background.SyncJobType.SubmitDocument,
                    User.Identity?.Name ?? "System",
                    EINVWORLD.Services.Background.SyncJobPayload.CreateForInvoice(invoiceNo));

                return new JsonResult(new
                {
                    success = false,
                    message = $"Error submitting Invoice: {ex.Message} A retry has been queued automatically."
                });
            }
        }

        private bool IsSelfBilled(string docTypeCode)
        {
            return new[] { "11", "12", "13", "14" }.Contains(docTypeCode);
        }

        // ✅ Updated signature
        private InputModels.InvoiceHeader CreateInvoiceHeader(
            PartyInfo supplier,
            PartyInfo? customer,
            eInvWorld.Models.InputModel.PublicCustomer? publicCustomer)
        {
            var invoiceHeader = new InputModels.InvoiceHeader
            {
                InvoiceNo = Invoice.InvoiceNo ?? "",
                RefDocumentNo = Invoice.RefDocumentNo,
                IssueDate = Invoice.IssueDate,
                DocTypeCode = Invoice.DocTypeCode,
                InvoicePeriod = Invoice.InvoicePeriod,
                Currency = Invoice.Currency,
                ForeignCurrency = Invoice.ForeignCurrency,
                ExchangeRate = Invoice.ExchangeRate,
                UUID = Invoice.UUID,
                PoDoNo = Invoice.PoDoNo,
                OriginalInvoiceDate = Invoice.OriginalInvoiceDate,
                Attention = Invoice.Attention,
                BankName = Invoice.BankName,
                BankAccountNo = Invoice.BankAccountNo,
                PaymentTerms = Invoice.PaymentTerms ?? ""
            };

            _logger.LogDebug("CreateInvoiceHeader: Mapped UUID={UUID}", invoiceHeader.UUID);

            if (IsSelfBilled(Invoice.DocTypeCode))
            {
                _logger.LogInformation("Self-Billed Document detected. Assigning General TIN as Supplier.");

                invoiceHeader.Customer = supplier;
                invoiceHeader.CustomerId = supplier.PartyInfoId;

                var generalTIN = _context.PartyInfos
                    .FirstOrDefault(p => p.TIN == "EI00000000010");

                if (generalTIN != null)
                {
                    invoiceHeader.Supplier = generalTIN;
                    _logger.LogInformation("Assigned General TIN as Supplier: {TIN} - {CompanyName}", generalTIN.TIN, generalTIN.CompanyName);
                }
                else
                {
                    _logger.LogError("No General TIN found! Self-billed document may be invalid.");
                    invoiceHeader.Supplier = supplier;
                }

                // PublicCustomer normally not used in self-billed flow
                invoiceHeader.PublicCustomer = null;
                invoiceHeader.PublicCustomerId = null;
            }
            else
            {
                // Standard Invoice Flow
                invoiceHeader.Supplier = supplier;

                if (customer != null)
                {
                    invoiceHeader.Customer = customer;
                    invoiceHeader.CustomerId = customer.PartyInfoId;

                    invoiceHeader.PublicCustomer = null;
                    invoiceHeader.PublicCustomerId = null;
                }
                else if (publicCustomer != null)
                {
                    invoiceHeader.Customer = null!;
                    invoiceHeader.CustomerId = null;

                    invoiceHeader.PublicCustomer = publicCustomer;
                    invoiceHeader.PublicCustomerId = publicCustomer.PublicCustomerId;
                }
            }

            // Totals
            invoiceHeader.TotalDiscountAmount = Invoice.TotalDiscountAmount;
            invoiceHeader.StartDate = Invoice.StartDate;
            invoiceHeader.EndDate = Invoice.EndDate;
            invoiceHeader.TotalAmountExclTax = Invoice.TotalAmountExclTax;
            invoiceHeader.TotalTaxAmount = Invoice.TotalTaxAmount;
            invoiceHeader.TotalAmountIncTax = Invoice.TotalAmountIncTax;
            invoiceHeader.TotalPayableAmount = Invoice.TotalPayableAmount;
            invoiceHeader.TotalNetAmount = Invoice.TotalNetAmount;

            // Invoice Lines
            invoiceHeader.InvoiceLines = Invoice.InvoiceLines.Select(viewLine =>
            {
                var line = new InputModels.InvoiceLine
                {
                    LineNumber = viewLine.LineNumber,
                    Quantity = viewLine.Quantity,
                    ItemCode = viewLine.ItemCode,
                    ItemDescription = viewLine.ItemDescription,
                    UnitOfMeasure = viewLine.UnitOfMeasure,
                    UnitPrice = viewLine.UnitPrice,
                    DiscountAmount = viewLine.DiscountAmount,
                    ClassificationCode = viewLine.ClassificationCode,
                    InvoiceHeader = invoiceHeader,
                    InvoiceTaxes = viewLine.Taxes.Select(tax => new InputModels.InvoiceTax
                    {
                        TaxCategory = tax.TaxCategory,
                        TaxPercentage = tax.TaxPercentage,
                        TaxAmount = tax.TaxAmount,
                        TaxExemptionReason = tax.TaxCategory == "E" &&
                                             string.IsNullOrWhiteSpace(tax.TaxExemptionReason)
                            ? "Tax exempted as per applicable regulations"
                            : tax.TaxExemptionReason ?? ""
                    }).ToList()
                };

                line.CalculateAmounts();
                return line;
            }).ToList();

            return invoiceHeader;
        }


        private async Task<IActionResult> SaveAsTemplateAsync(string TemplateName)
        {
            _logger.LogDebug("SaveAsTemplateAsync - TemplateName: '{TemplateName}'", TemplateName);
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (Invoice == null)
                    Invoice = new InvoiceHeaderView();

                _logger.LogDebug("SaveAsTemplateAsync - DocType={DocType}, Currency={Currency}, Lines={Lines}",
                    Invoice.DocTypeCode, Invoice.Currency, Invoice.InvoiceLines?.Count ?? 0);

                var template = _invoiceTemplateService.CreateTemplateFromInvoice(Invoice, TemplateName, userId ?? "", _context);

                _context.InvoiceTemplates.Add(template);
                await _context.SaveChangesAsync();

                bool isAjaxRequest = Request.Headers["X-Requested-With"].ToString().Contains("XMLHttpRequest");
                _logger.LogDebug("SaveAsTemplateAsync - Success, AJAX={IsAjax}", isAjaxRequest);

                if (isAjaxRequest)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        message = "Your invoice template has been saved and can be reused for future invoices."
                    });
                }
                else
                {
                    // Return page for regular form submissions
                    ViewData["Message"] = "Your invoice template has been saved and can be reused for future invoices.";
                    ViewData["ShowReviewTab"] = true;
                    await LoadDropdownDataAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to save template.");

                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"].ToString().Contains("XMLHttpRequest");

                if (isAjaxRequest)
                {
                    // Return JSON error response for AJAX requests
                    return new JsonResult(new
                    {
                        success = false,
                        message = "An error occurred while saving the template."
                    });
                }
                else
                {
                    // Return page with error for regular form submissions
                    ModelState.AddModelError(string.Empty, "An error occurred while saving the template.");
                    return Page();
                }
            }
        }

        public static DateTime ToMalaysiaTime(DateTime utcDateTime)
        {
            var malaysiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), malaysiaTimeZone);
        }
        public async Task<IActionResult> OnGetLoadPartyDetailsAsync(int partyId, string partyType)
        {
            try
            {
                object? partyData = null;

                // 1. Fetch from PartyInfo (Standard Registered Companies)
                if (partyType == "PI")
                {
                    partyData = await _context.PartyInfos
                        .Where(p => p.PartyInfoId == partyId)
                        .Select(p => new
                        {
                            PartyInfoId = p.PartyInfoId,
                            TIN = p.TIN,
                            BankAccountNo = p.BankAccountNo,
                            BankName = p.BankName,
                            Attention = p.Attention,
                            PaymentTerms = p.PaymentTerms ?? "", // ✅ ADD THIS
                            Email = p.Email,
                            Address = p.Addr1 + " " + (p.Addr2 ?? "") + " " + (p.CityName ?? "")
                        })
                        .FirstOrDefaultAsync();
                }
                // 2. Fetch from PublicCustomer (New Logic)
                else if (partyType == "PC")
                {
                    partyData = await _context.PublicCustomers
                        .Where(p => p.PublicCustomerId == partyId)
                        .Select(p => new
                        {
                            PartyInfoId = p.PublicCustomerId,
                            TIN = p.TIN,
                            BankAccountNo = p.BankAccountNo,
                            BankName = p.BankName,
                            Attention = p.Attention,
                            PaymentTerms = p.PaymentTerms ?? "", // ✅ ADD THIS
                            Email = p.Email,
                            Address = p.Addr1 + " " + (p.Addr2 ?? "") + " " + (p.CityName ?? "")
                        })
                        .FirstOrDefaultAsync();
                }

                if (partyData == null)
                    return new JsonResult(new { success = false, message = "Details not found" });

                return new JsonResult(new { success = true, data = partyData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading party details");
                return new JsonResult(new { success = false, message = "An error occurred." });
            }
        }





        public async Task<IActionResult> OnGetFilterSuppliersAsync(string docTypeCode = "")
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompanies = _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Include(uc => uc.PartyInfo)
                    .ToList();

                bool isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(docTypeCode);

                List<object> suppliers;

                if (isSelfBilled)
                {
                    // For self-billed documents, only show General TINs
                    suppliers = _context.PartyInfos
                        .Where(p => p.TIN == "EI00000000010" || p.TIN == "EI00000000030")
                        .Select(p => new { value = p.PartyInfoId.ToString(), text = p.CompanyName })
                        .Cast<object>()
                        .ToList();
                }
                else
                {
                    // For regular documents, only show user companies (normal companies)
                    suppliers = _context.PartyInfos
                        .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId))
                        .Select(p => new { value = p.PartyInfoId.ToString(), text = p.CompanyName })
                        .Cast<object>()
                        .ToList();
                }

                return new JsonResult(suppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering suppliers for docTypeCode: {DocTypeCode}", docTypeCode);
                return new JsonResult(new List<object>());
            }
        }

        public async Task<IActionResult> OnGetLoadCustomersAsync(int supplierId, string docTypeCode = "")
        {
            try
            {
                _logger.LogDebug("OnGetLoadCustomersAsync: SupplierId={SupplierId}, DocTypeCode='{DocTypeCode}'", supplierId, docTypeCode);

                if (supplierId <= 0)
                    return new JsonResult(new List<object>());

                bool isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(docTypeCode);

                if (isSelfBilled)
                {
                    // For self-billed documents, the buyer must be one of the user's own companies
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var userCompanies = await _context.UserCompanies
                        .Where(uc => uc.UserId == userId)
                        .Include(uc => uc.PartyInfo)
                        .ToListAsync();

                    var resultList = userCompanies.Select(uc => new SelectListItem
                    {
                        Value = $"PI_{uc.PartyInfoId}",
                        Text = uc.PartyInfo.CompanyName
                    }).ToList();

                    _logger.LogDebug("Self-billed buyer count: {Count}", resultList.Count);
                    return new JsonResult(resultList);
                }
                else
                {
                    var resultList = await _buyerService
                                            .GetCombinedBuyersBySupplierAsync(supplierId);

                    _logger.LogDebug("Buyer count: {Count}", resultList?.Count ?? 0);

                    return new JsonResult(resultList ?? new List<SelectListItem>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers for supplierId: {SupplierId}", supplierId);
                return new JsonResult(new List<object>());
            }
        }

        private async Task<IActionResult> UpdateTemplateAsync()
        {
            try
            {
                _logger.LogDebug("UpdateTemplateAsync - TemplateName: '{TemplateName}'", TemplateName);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Find the existing template
                var existingTemplate = await _context.InvoiceTemplates
                    .Include(t => t.InvoiceLines)
                    .ThenInclude(l => l.Taxes)
                    .FirstOrDefaultAsync(t => t.TemplateName == TemplateName && t.CreatedByUserId == userId);

                if (existingTemplate == null)
                {
                    ModelState.AddModelError(string.Empty, "Template not found for updating.");
                    return Page();
                }

                // Update template with current form data
                existingTemplate.RefDocumentNo = Invoice.RefDocumentNo;
                existingTemplate.DocTypeCode = Invoice.DocTypeCode;
                existingTemplate.SupplierId = Invoice.SupplierId > 0 ? Invoice.SupplierId : null;
                existingTemplate.CustomerId = Invoice.CustomerId > 0 ? Invoice.CustomerId : null;
                existingTemplate.PublicCustomerId = Invoice.PublicCustomerId > 0 ? Invoice.PublicCustomerId : null;
                existingTemplate.Currency = Invoice.Currency;
                existingTemplate.ExchangeRate = Invoice.ExchangeRate;
                existingTemplate.ForeignCurrency = Invoice.ForeignCurrency;
                existingTemplate.StartDate = Invoice.StartDate;
                existingTemplate.EndDate = Invoice.EndDate;
                existingTemplate.InvoicePeriod = Invoice.InvoicePeriod;
                existingTemplate.LastUpdated = DateTime.UtcNow;

                // Remove existing lines and taxes
                _context.InvoiceTemplateTaxes.RemoveRange(
                    existingTemplate.InvoiceLines.SelectMany(l => l.Taxes));
                _context.InvoiceTemplateLines.RemoveRange(existingTemplate.InvoiceLines);

                // Add updated lines
                existingTemplate.InvoiceLines = Invoice.InvoiceLines?.Select((line, i) =>
                {
                    var templateLine = new InvoiceTemplateLine
                    {
                        ClassificationCode = line.ClassificationCode,
                        ItemCode = line.ItemCode,
                        ItemDescription = line.ItemDescription,
                        Quantity = line.Quantity,
                        UnitOfMeasure = line.UnitOfMeasure,
                        UnitPrice = line.UnitPrice,
                        AmountExclTax = line.Subtotal // Map to the correct property
                    };

                    templateLine.Taxes = line.Taxes?.Select(tax => new InvoiceTemplateTax
                    {
                        TaxCategory = tax.TaxCategory,
                        TaxPercentage = tax.TaxPercentage,
                        TaxAmount = tax.TaxAmount,
                        TaxExemptionReason = tax.TaxExemptionReason
                    }).ToList() ?? new List<InvoiceTemplateTax>();

                    return templateLine;
                }).ToList() ?? new List<InvoiceTemplateLine>();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Template updated successfully: {TemplateName}", TemplateName);
                ViewData["Message"] = $"Template '{TemplateName}' has been updated successfully.";
                await LoadDropdownDataAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to update template: {TemplateName}", TemplateName);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the template.");
                await LoadDropdownDataAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetCheckTemplateNameAsync(string templateName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    return new JsonResult(new { exists = true, message = "Template name cannot be empty." });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var existingTemplate = await _context.InvoiceTemplates
                    .Where(t => t.TemplateName == templateName && t.CreatedByUserId == userId)
                    .FirstOrDefaultAsync();

                if (existingTemplate != null)
                {
                    return new JsonResult(new
                    {
                        exists = true,
                        message = "A template with this name already exists. Please choose a different name."
                    });
                }

                return new JsonResult(new
                {
                    exists = false,
                    message = "Template name is available."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking template name: {TemplateName}", templateName);
                return new JsonResult(new
                {
                    exists = true,
                    message = "Error checking template name. Please try again."
                });
            }
        }

        private bool InvoiceHeaderExists(string id)
        {
            return _context.InvoiceHeaders.Any(e => e.InvoiceNo == id);
        }

        private async Task<IActionResult> LoadTemplateForEditingAsync(int templateId)
        {
            try
            {
                _logger.LogInformation("Loading template for editing: {TemplateId}", templateId);

                // Load existing template with related data
                var existingTemplate = await _context.InvoiceTemplates
                    .Include(t => t.InvoiceLines)
                    .ThenInclude(l => l.Taxes)
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (existingTemplate == null)
                {
                    return NotFound($"Template {templateId} not found.");
                }

                // Set template mode
                IsTemplateMode = true;
                TemplateName = existingTemplate.TemplateName;

                // Map template to invoice view model for editing
                Invoice = new InvoiceHeaderView
                {
                    TemplateName = existingTemplate.TemplateName,
                    RefDocumentNo = existingTemplate.RefDocumentNo,
                    DocTypeCode = existingTemplate.DocTypeCode,
                    SupplierId = existingTemplate.SupplierId ?? 0,

                    CustomerId = existingTemplate.CustomerId ?? 0,
                    PublicCustomerId = existingTemplate.PublicCustomerId ?? 0,

                    Currency = existingTemplate.Currency ?? "MYR",
                    ExchangeRate = existingTemplate.ExchangeRate,
                    ForeignCurrency = existingTemplate.ForeignCurrency ?? existingTemplate.Currency ?? "MYR",
                    StartDate = existingTemplate.StartDate,
                    EndDate = existingTemplate.EndDate,
                    InvoicePeriod = existingTemplate.InvoicePeriod,
                    IssueDate = DateTime.UtcNow,
                    InvoiceNo = "",
                    InvoiceLines = existingTemplate.InvoiceLines?.Select((line, i) => new InvoiceLineView
                    {
                        LineNumber = i + 1,
                        ClassificationCode = line.ClassificationCode,
                        ItemCode = line.ItemCode,
                        ItemDescription = line.ItemDescription,
                        Quantity = line.Quantity,
                        UnitOfMeasure = line.UnitOfMeasure,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.AmountExclTax,
                        Taxes = line.Taxes?.Select(tax => new InvoiceTaxView
                        {
                            TaxCategory = tax.TaxCategory,
                            TaxPercentage = tax.TaxPercentage,
                            TaxAmount = tax.TaxAmount,
                            TaxExemptionReason = tax.TaxExemptionReason
                        }).ToList() ?? new List<InvoiceTaxView>()
                    }).ToList() ?? new List<InvoiceLineView>()
                };

                if (existingTemplate.CustomerId > 0)
                {
                    SelectedBuyerId = $"PI_{existingTemplate.CustomerId}";
                }
                else if (existingTemplate.PublicCustomerId > 0)
                {
                    SelectedBuyerId = $"PC_{existingTemplate.PublicCustomerId}";
                }

                // Calculate totals for template
                foreach (var line in Invoice.InvoiceLines)
                {
                    line.CalculateAmounts();
                }
                Invoice.CalculateInvoiceTotals();

                // Load dropdown data
                await LoadDropdownDataAsync();
                BankDetails = await LoadBankDetailsAsync();

                _logger.LogInformation("Template loaded successfully for editing: {TemplateName}", existingTemplate.TemplateName);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template for editing: {TemplateId}", templateId);
                return NotFound($"Error loading template {templateId}.");
            }
        }

        private JsonResult AjaxFail(string message, int statusCode = 400, object? extra = null)
        {
            Response.StatusCode = statusCode;

            if (extra == null)
                return new JsonResult(new { success = false, message });

            return new JsonResult(new { success = false, message, extra });
        }

    }
}