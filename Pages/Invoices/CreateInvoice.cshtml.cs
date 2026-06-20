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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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

namespace EINVWORLD.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier")]
    public class CreateInvoiceModel : SupplierBasePage
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

        public CreateInvoiceModel(
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
            IBuyerService buyerService) : base(context)
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
        }

        [BindProperty]
        public InvoiceHeaderView Invoice { get; set; } = null!;
        public List<SelectListItem> CurrencyCodes { get; set; } = new();
        public List<SelectListItem> InvoicePeriodOptions { get; set; } = new();
        public List<SelectListItem> Suppliers { get; set; } = new();
        public List<SelectListItem> Customers { get; set; } = new();
        public List<EInvoiceType> EInvoiceTypes { get; set; } = new();
        public List<SelectListItem> ClassificationCodes { get; set; } = new();
        public List<SelectListItem> UnitOptions { get; set; } = new();
        public List<SelectListItem> TaxCategoryOptions { get; set; } = new();
        public List<SelectListItem> SavedItems { get; set; } = new List<SelectListItem>();

        [BindProperty]
        public string? SelectedBuyerId { get; set; }

        public string? GeneratedJson { get; set; }
        public string? Message { get; set; }
        public string? SubmissionResult { get; private set; }
        public int? PrimaryCompanyId { get; set; } // Store Primary Supplier ID
        public List<object> BankDetails { get; set; } = new List<object>(); // Store bank details

        [BindProperty]
        public string? TemplateName { get; set; }
        public async Task OnGetAsync(int? templateId = null, string? invoiceNo = null, string? uuid = null, string? type = null, string? cloneId = null)
        {
            _logger.LogInformation("🚀 OnGetAsync called with parameters - templateId: {templateId}, invoiceNo: {invoiceNo}, uuid: {uuid}, type: {type}, cloneId: {cloneId}", templateId, invoiceNo, uuid, type, cloneId);

            // Load all dropdown options
            ClassificationCodes = _dropdownHelper.GetClassificationCodeOptions();
            UnitOptions = _dropdownHelper.GetUnitOptions();
            TaxCategoryOptions = _dropdownHelper.GetTaxCategoryOptions();

            _logger.LogDebug("Dropdown counts: ClassificationCodes={ClassificationCodes}, Units={Units}, TaxCategories={TaxCategories}",
                ClassificationCodes?.Count ?? 0, UnitOptions?.Count ?? 0, TaxCategoryOptions?.Count ?? 0);

            int backdateSeconds = _configuration.GetValue<int>("InvoiceSettings:BackdateSeconds", 0);
            var currentUtcTime = DateTime.UtcNow.AddSeconds(-backdateSeconds);
            var malaysiaTime = TimeZoneInfo.ConvertTimeFromUtc(currentUtcTime, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

            string classificationCode = "022";

            if (Invoice != null && new[] { "11", "12", "13", "14" }.Contains(Invoice.DocTypeCode))
            {
                var selectedSupplier = _context.PartyInfos.FirstOrDefault(p => p.PartyInfoId == Invoice.SupplierId);
                classificationCode = "004";
            }

            string[] adjustmentTypes = { "CN", "DN", "RN", "SELF-CN", "SELF-DN", "SELF-RN" };
            bool isAdjustmentDocCreation = !string.IsNullOrEmpty(type) && adjustmentTypes.Contains(type.ToUpper()) && !string.IsNullOrEmpty(uuid);

            // Resend
            if (!string.IsNullOrEmpty(cloneId))
            {
                _logger.LogInformation("🔄 Cloning invoice from cloneId: {cloneId}", cloneId);

                var oldInvoice = await _context.InvoiceHeaders
                    .Include(i => i.InvoiceLines)
                        .ThenInclude(l => l.InvoiceTaxes)
                    .FirstOrDefaultAsync(i => i.InvoiceNo == cloneId);

                if (oldInvoice != null)
                {
                    Invoice = new InvoiceHeaderView
                    {
                        // Copy the required references. Added ?? 0 to fix nullable int? error
                        SupplierId = oldInvoice.SupplierId ?? 0,
                        CustomerId = oldInvoice.CustomerId,
                        PublicCustomerId = oldInvoice.PublicCustomerId,
                        DocTypeCode = oldInvoice.DocTypeCode,
                        Currency = oldInvoice.Currency,
                        ExchangeRate = oldInvoice.ExchangeRate,
                        ForeignCurrency = oldInvoice.ForeignCurrency ?? oldInvoice.Currency ?? "MYR",
                        PaymentTerms = oldInvoice.PaymentTerms,
                        Attention = oldInvoice.Attention,
                        BankAccountNo = oldInvoice.BankAccountNo,
                        BankName = oldInvoice.BankName,
                        InvoicePeriod = oldInvoice.InvoicePeriod,
                        StartDate = oldInvoice.StartDate,
                        EndDate = oldInvoice.EndDate,
                        PoDoNo = oldInvoice.PoDoNo,
                        RefDocumentNo = oldInvoice.RefDocumentNo,

                        // ⚠️ CRITICAL: Generate a brand NEW ID and Reset the Date
                        InvoiceNo = GenerateNextInvoiceNumber(),
                        IssueDate = malaysiaTime,

                        // Map old lines to new View lines using CORRECT PROPERTY NAMES
                        InvoiceLines = oldInvoice.InvoiceLines.Select(line => new InvoiceLineView
                        {
                            LineNumber = line.LineNumber,
                            ClassificationCode = line.ClassificationCode ?? "022",
                            ItemDescription = line.ItemDescription,
                            Quantity = line.Quantity,
                            UnitOfMeasure = line.UnitOfMeasure ?? "XUN",
                            UnitPrice = line.UnitPrice,
                            DiscountAmount = line.DiscountAmount ?? 0,
                            Subtotal = line.Subtotal ?? 0,
                            AmountExclTax = line.AmountExclTax,
                            AmountInclTax = line.AmountInclTax ?? 0,

                            Taxes = line.InvoiceTaxes.Select(tax => new InvoiceTaxView
                            {
                                TaxCategory = tax.TaxCategory,
                                TaxPercentage = tax.TaxPercentage,
                                TaxAmount = tax.TaxAmount,
                                TaxExemptionReason = tax.TaxExemptionReason
                            }).ToList()
                        }).ToList()
                    };

                    // Optional: Alert the user that they are editing a cloned copy
                    TempData["SuccessMessage"] = $"Data successfully copied from {cloneId}. Please review and submit as a new e-Invoice.";
                }
            }


            if (Invoice == null && !isAdjustmentDocCreation)
            {
                Invoice = new InvoiceHeaderView
                {
                    InvoiceNo = GenerateNextInvoiceNumber(),
                    InvoicePeriod = InvoicePeriodEnum.Not_Applicable,
                    RefDocumentNo = "NA",
                    DocTypeCode = "01",
                    IssueDate = malaysiaTime,
                    InvoiceLines = new List<InvoiceLineView>
            {
                new InvoiceLineView {
                    LineNumber = 1,
                    ClassificationCode = classificationCode,
                    ItemCode = "",
                    ItemDescription = "",
                    Quantity = (decimal?)0.00,
                    UnitOfMeasure = "XUN",
                    UnitPrice = 0.00m,
                    Taxes = new List<InvoiceTaxView> {
                        new InvoiceTaxView { TaxCategory = "01", TaxPercentage = 0, TaxAmount = 0.00m }
                    }
                }
            }
                };
            }
            else if (Invoice == null && isAdjustmentDocCreation)
            {
                Invoice = new InvoiceHeaderView
                {
                    InvoicePeriod = InvoicePeriodEnum.Not_Applicable,
                    RefDocumentNo = "NA",
                    InvoiceLines = new List<InvoiceLineView>()
                };
                _logger.LogInformation("🔧 Initialized minimal Invoice object for adjustment document creation");
            }
            else if (Invoice != null && Invoice.InvoicePeriod == default)
            {
                Invoice.InvoicePeriod = InvoicePeriodEnum.Not_Applicable;
            }

            // ✅ MOVED UP: Populate Adjustment Doc BEFORE setting Default User Companies or pre-selecting Buyer
            if (!string.IsNullOrEmpty(type) && adjustmentTypes.Contains(type.ToUpper()) && !string.IsNullOrEmpty(uuid))
            {
                _logger.LogInformation("🔄 Creating {type} from existing invoice - UUID: {uuid}, InvoiceNo: {invoiceNo}", type.ToUpper(), uuid, invoiceNo);
                await PopulateAdjustmentDocumentFromOriginalInvoice(uuid, invoiceNo, type.ToUpper());
                _logger.LogInformation("✅ {type} population completed. RefUUID value: {refUuid}", type.ToUpper(), Invoice?.RefUUID);
            }

            // ✅ MOVED UP: Populate Template BEFORE setting Default User Companies
            if (templateId.HasValue)
            {
                var template = _context.InvoiceTemplates
                    .Include(t => t.InvoiceLines)
                    .ThenInclude(l => l.Taxes)
                    .FirstOrDefault(t => t.Id == templateId.Value);

                if (template != null)
                {
                    foreach (var line in template.InvoiceLines)
                        line.CalculateAmounts();

                    Invoice = new InvoiceHeaderView
                    {
                        TemplateName = template.TemplateName,
                        RefDocumentNo = template.RefDocumentNo,
                        DocTypeCode = template.DocTypeCode,
                        SupplierId = template.SupplierId ?? 0,
                        CustomerId = template.CustomerId ?? 0,
                        PublicCustomerId = template.PublicCustomerId ?? 0, // if exists
                        Currency = template.Currency ?? "MYR",
                        ExchangeRate = template.ExchangeRate,
                        ForeignCurrency = template.ForeignCurrency ?? template.Currency ?? "MYR",
                        StartDate = template.StartDate,
                        OriginalInvoiceDate = template.OriginalInvoiceDate,
                        PoDoNo = template.PoDoNo,
                        EndDate = template.EndDate,
                        InvoicePeriod = template.InvoicePeriod,
                        IssueDate = DateTime.UtcNow,
                        InvoiceNo = GenerateNextInvoiceNumber(),
                        InvoiceLines = template.InvoiceLines.Select((line, i) => new InvoiceLineView
                        {
                            LineNumber = i + 1,
                            ClassificationCode = line.ClassificationCode,
                            ItemCode = line.ItemCode,
                            ItemDescription = line.ItemDescription,
                            Quantity = line.Quantity,
                            UnitOfMeasure = line.UnitOfMeasure,
                            UnitPrice = line.UnitPrice,
                            DiscountAmount = line.DiscountAmount,
                            Subtotal = line.Subtotal,
                            AmountExclTax = line.AmountExclTax,
                            AmountInclTax = line.AmountInclTax,
                            Taxes = line.Taxes.Select(tax => new InvoiceTaxView
                            {
                                TaxCategory = tax.TaxCategory,
                                TaxPercentage = tax.TaxPercentage,
                                TaxAmount = tax.TaxAmount,
                                TaxExemptionReason = tax.TaxExemptionReason
                            }).ToList()
                        }).ToList()
                    };

                    TemplateName = template.TemplateName;
                }
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

                Suppliers = _context.PartyInfos
                    .Where(p => userCompanies.Select(uc => uc.PartyInfoId).Contains(p.PartyInfoId)
                             || p.TIN == "EI00000000010" || p.TIN == "EI00000000030")
                    .Select(p => new SelectListItem { Value = p.PartyInfoId.ToString(), Text = p.CompanyName })
                    .ToList();

                // ✅ ADDED CHECK: Only apply defaults if Supplier hasn't been set by an Original Invoice or Template
                if (Invoice != null && Invoice.SupplierId == 0)
                {
                    if (new[] { "11", "12", "13", "14" }.Contains(Invoice.DocTypeCode))
                    {
                        _logger.LogInformation("Self-Billed Invoice detected. Switching Supplier and Buyer.");
                        Invoice.CustomerId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;

                        var generalTIN = _context.PartyInfos
                            .Where(p => p.TIN == "EI00000000010")
                            .FirstOrDefault();

                        if (generalTIN != null)
                        {
                            Invoice.SupplierId = generalTIN.PartyInfoId;
                            _logger.LogInformation("Assigned General TIN as Supplier: {TIN} - {CompanyName}", generalTIN.TIN, generalTIN.CompanyName);
                        }
                        else
                        {
                            _logger.LogWarning("No General TIN found!");
                            Invoice.SupplierId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;
                        }
                    }
                    else
                    {
                        Invoice.SupplierId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;
                    }
                }

                Customers = _context.PartyInfos
                    .Where(p => _context.SupplierBuyers.Any(sb => sb.SupplierId == Invoice!.SupplierId && sb.BuyerId == p.PartyInfoId)
                             || p.TIN == "EI00000000010" || p.TIN == "EI00000000020" || p.TIN == "EI00000000040")
                    .Select(p => new SelectListItem { Value = p.PartyInfoId.ToString(), Text = p.CompanyName })
                    .ToList();
            }
            else
            {
                Suppliers = new List<SelectListItem>();
                Customers = new List<SelectListItem>();
            }

            // ✅ MODIFIED: Async dropdown loader
            await LoadDropdownDataAsync();

            // ✅ NEW: Pre-select Buyer (Now runs AFTER PopulateAdjustment so the IDs are known!)
            if (Invoice != null)
            {
                if (Invoice.CustomerId > 0)
                {
                    SelectedBuyerId = $"PI_{Invoice.CustomerId}";
                }
                else if (Invoice.PublicCustomerId > 0)
                {
                    SelectedBuyerId = $"PC_{Invoice.PublicCustomerId}";
                }
            }

            BankDetails = await LoadBankDetailsAsync();
        }


        private async Task LoadDropdownDataAsync()
        {
            EInvoiceTypes = _context.EInvoiceTypes
                .Where(e => e.IsActive)
                .ToList();

            CurrencyCodes = _context.CurrencyCodes
                .Where(c => c.IsActive)
                .Select(c => new SelectListItem
                {
                    Text = $"{c.Currency} ({c.Code})",
                    Value = c.Code
                })
                .ToList();

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

                // ✅ NEW: Populate Saved Items for Quick Select using the PrimaryCompanyId
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

                // Only apply defaults if Supplier hasn't been set by an Original Invoice or Template
                if (Invoice.SupplierId == 0)
                {
                    // Preserve original self-billed switching logic
                    if (new[] { "11", "12", "13", "14" }.Contains(Invoice.DocTypeCode))
                    {
                        Invoice.CustomerId = primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId;

                        var generalTIN = await _context.PartyInfos
                            .Where(p => p.TIN == "EI00000000010")
                            .FirstOrDefaultAsync();

                        Invoice.SupplierId =
                            generalTIN?.PartyInfoId
                            ?? (primaryCompany?.PartyInfoId ?? userCompanies.First().PartyInfoId);
                    }
                    else
                    {
                        Invoice.SupplierId =
                            primaryCompany?.PartyInfoId
                            ?? userCompanies.First().PartyInfoId;
                    }
                }

                if (Invoice.SupplierId > 0)
                {
                    bool isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(Invoice.DocTypeCode);
                    if (isSelfBilled)
                    {
                        Customers = userCompanies.Select(uc => new SelectListItem
                        {
                            Value = $"PI_{uc.PartyInfoId}",
                            Text = uc.PartyInfo.CompanyName
                        }).ToList();
                    }
                    else
                    {
                        Customers = await _buyerService
                            .GetCombinedBuyersBySupplierAsync(Invoice.SupplierId);
                    }
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
                SavedItems = new List<SelectListItem>(); // Clear if no company
            }
        }


        // Method to load bank details for suppliers
        private async Task<List<object>> LoadBankDetailsAsync()
        {
            _logger.LogDebug("LoadBankDetailsAsync called");
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

        private async Task PopulateAdjustmentDocumentFromOriginalInvoice(string originalUuid, string? originalInvoiceNo, string adjustmentType)
        {
            try
            {
                _logger.LogInformation("🔄 Populating {type} from original invoice UUID: {uuid}", adjustmentType, originalUuid);

                // Get the original invoice with all related data
                var originalInvoice = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer)
                    .Include(i => i.InvoiceLines)
                        .ThenInclude(l => l.InvoiceTaxes)
                    .FirstOrDefaultAsync(i => i.UUID == originalUuid);

                if (originalInvoice == null)
                {
                    _logger.LogWarning("❌ Original invoice not found for UUID: {uuid}", originalUuid);
                    return;
                }

                _logger.LogInformation("✅ Found original invoice: {invoiceNo}", originalInvoice.InvoiceNo);

                // Set document type based on adjustment type
                var docTypeCodes = new Dictionary<string, string>
                {
                    { "CN", "02" }, // Credit Note
                    { "DN", "03" }, // Debit Note
                    { "RN", "04" }, // Refund Note
                    { "SELF-CN", "12" }, // Self-billed Credit Note
                    { "SELF-DN", "13" }, // Self-billed Debit Note
                    { "SELF-RN", "14" }  // Self-billed Refund Note
                };

                Invoice.DocTypeCode = docTypeCodes[adjustmentType];
                _logger.LogInformation("📋 Document type set to {type} ({code})", adjustmentType, docTypeCodes[adjustmentType]);

                // Set RefUUID to reference the original invoice
                Invoice.RefUUID = originalUuid;
                _logger.LogInformation("🔗 Set RefUUID to: {refUuid}", originalUuid);
                _logger.LogInformation("🔍 Invoice.RefUUID after setting: {refUuidValue}", Invoice.RefUUID);

                // Copy supplier and customer information
                Invoice.SupplierId = originalInvoice.SupplierId ?? 0;
                Invoice.CustomerId = originalInvoice.CustomerId ?? 0;
                Invoice.PublicCustomerId = originalInvoice.PublicCustomerId ?? 0;
                // Copy other invoice details
                Invoice.Currency = originalInvoice.Currency;
                Invoice.ForeignCurrency = originalInvoice.ForeignCurrency ?? Invoice.Currency ?? "MYR";
                Invoice.ExchangeRate = originalInvoice.ExchangeRate;

                // Set issue date to today for the adjustment document
                Invoice.IssueDate = DateTime.Now.Date;

                // Copy payment terms and other details
                Invoice.PaymentTerms = originalInvoice.PaymentTerms;
                Invoice.BankAccountNo = originalInvoice.BankAccountNo;
                Invoice.BankName = originalInvoice.BankName;
                Invoice.Attention = originalInvoice.Attention;

                // Copy invoice lines with appropriate amounts based on adjustment type
                if (originalInvoice.InvoiceLines != null && originalInvoice.InvoiceLines.Any())
                {
                    var adjustmentPrefixes = new Dictionary<string, string>
                    {
                        { "CN", "Credit for:" },
                        { "DN", "Debit adjustment for:" },
                        { "RN", "Refund for:" },
                        { "SELF-CN", "Self-billed credit for:" },
                        { "SELF-DN", "Self-billed debit adjustment for:" },
                        { "SELF-RN", "Self-billed refund for:" }
                    };

                    // Credit Notes and Refund Notes use negative amounts, Debit Notes use positive amounts
                    var amountMultiplier = (adjustmentType == "DN" || adjustmentType == "SELF-DN") ? 1 : -1;

                    Invoice.InvoiceLines = originalInvoice.InvoiceLines.Select((line, index) => new InvoiceLineView
                    {
                        LineNumber = index + 1, // ✅ CRITICAL: Set LineNumber for proper display
                        ItemCode = line.ItemCode ?? "",
                        ItemDescription = $"{adjustmentPrefixes[adjustmentType]} {line.ItemDescription ?? "Item"}",
                        Quantity = line.Quantity ?? 1, // Keep original quantity, default to 1 if null
                        UnitOfMeasure = string.IsNullOrWhiteSpace(line.UnitOfMeasure) || line.UnitOfMeasure == "Unit" ? "XUN" : line.UnitOfMeasure,
                        UnitPrice = Math.Abs(line.UnitPrice ?? 0), // Show positive values in UI, credit logic handled by document type
                        ClassificationCode = line.ClassificationCode ?? "022",
                        DiscountAmount = (line.DiscountAmount ?? 0) * Math.Abs(amountMultiplier), // Discount always positive
                        Taxes = line.InvoiceTaxes?.Select(tax => new InvoiceTaxView
                        {
                            TaxCategory = tax.TaxCategory ?? "01",
                            TaxPercentage = tax.TaxPercentage ?? 0,
                            TaxAmount = (tax.TaxAmount ?? 0) * amountMultiplier // Adjust tax amount based on document type
                        }).ToList() ?? new List<InvoiceTaxView> {
                            new InvoiceTaxView { TaxCategory = "01", TaxPercentage = 0, TaxAmount = 0.00m }
                        }
                    }).ToList();

                    _logger.LogInformation("✅ Copied {lineCount} invoice lines with adjusted amounts for {type}", Invoice.InvoiceLines.Count, adjustmentType);
                }

                // Generate a new invoice number using the universal EINV numbering system
                // All document types should use EINV prefix for consistency
                var docTypeCode = docTypeCodes[adjustmentType];
                Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                _logger.LogInformation("📄 Generated {type} number: {docNumber}", adjustmentType, Invoice.InvoiceNo);

                _logger.LogInformation("🎉 {type} successfully populated from invoice {originalInvoiceNo}", adjustmentType, originalInvoiceNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error populating Credit Note from original invoice {uuid}", originalUuid);
            }
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            var isAjax = Request.Headers["X-Requested-With"].ToString().Contains("XMLHttpRequest");

            try
            {
                if (string.IsNullOrWhiteSpace(action))
                    action = Request.Form["invoiceAction"].ToString();

                // 🔥 HYBRID FIX: Parse SelectedBuyerId BEFORE any action logic (including saveAsTemplate)
                if (Invoice != null && !string.IsNullOrEmpty(SelectedBuyerId))
                {
                    var parts = SelectedBuyerId.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                    {
                        if (parts[0] == "PI")
                        {
                            Invoice.CustomerId = id;
                            Invoice.PublicCustomerId = null;
                        }
                        else if (parts[0] == "PC")
                        {
                            Invoice.PublicCustomerId = id;
                            Invoice.CustomerId = null;
                        }
                    }
                }

                // NOW safe to save template (buyer IDs already populated)
                if (action == "saveAsTemplate")
                {
                    var templateName = Request.Form["TemplateName"].ToString();
                    return await SaveAsTemplateAsync(templateName);
                }

                if (Invoice == null)
                    ModelState.AddModelError("", "Invoice payload is missing.");

                if (Invoice != null)
                {
                    if (string.IsNullOrWhiteSpace(Invoice.InvoiceNo))
                        Invoice.InvoiceNo = GenerateNextInvoiceNumber();

                    if (string.IsNullOrWhiteSpace(Invoice.ForeignCurrency))
                        Invoice.ForeignCurrency = Invoice.Currency ?? "MYR";
                }

                if (Invoice?.InvoiceLines == null || !Invoice.InvoiceLines.Any())
                    ModelState.AddModelError("", "At least one invoice item is required.");

                if (Invoice?.InvoiceLines != null && Invoice.InvoiceLines.Any(l => l.Taxes == null || !l.Taxes.Any()))
                    ModelState.AddModelError("", "Each invoice item must have at least one tax.");

                if (Invoice?.InvoiceLines != null)
                {
                    foreach (var line in Invoice.InvoiceLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line.ItemDescription))
                        {
                            line.ItemDescription = line.ItemDescription.Replace("\r\n", "\n").Replace("\r", "\n");
                        }
                    }
                }
                // ================================
                // Supplier / Buyer Resolution
                // ================================
                PartyInfo? supplier = null;
                PartyInfo? customer = null;
                PublicCustomer? publicCustomer = null;

                if (Invoice != null)
                {
                    supplier = _context.PartyInfos
                        .FirstOrDefault(p => p.PartyInfoId == Invoice.SupplierId);

                    if (supplier == null)
                        ModelState.AddModelError("", "Supplier information is missing.");

                    // 🔥 IDs already parsed above — just fetch
                    if (Invoice.CustomerId > 0)
                    {
                        customer = await _context.PartyInfos.FindAsync(Invoice.CustomerId);
                    }
                    else if (Invoice.PublicCustomerId > 0)
                    {
                        publicCustomer = await _context.PublicCustomers.FindAsync(Invoice.PublicCustomerId);
                    }

                    if (customer == null && publicCustomer == null)
                        ModelState.AddModelError("SelectedBuyerId", "Please select a valid buyer.");
                }

                // ================================
                // Classification Enforcement
                // ================================
                if (Invoice != null && supplier != null && Invoice.InvoiceLines != null)
                {
                    var isSelfBilled = IsSelfBilled(Invoice.DocTypeCode);
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
                        "010","011","033","034","035","036","037","038","039","040","041","045"
                    };

                            if (supplierTin == "EI00000000010")
                            {
                                if (!generalPublicValidCodes.Contains(line.ClassificationCode))
                                    ModelState.AddModelError("", "For self-billed with supplier: General Public, classification code must be 004.");
                            }
                            else if (supplierTin == "EI00000000030")
                            {
                                if (!foreignSupplierValidCodes.Contains(line.ClassificationCode))
                                    ModelState.AddModelError("", "For self-billed with supplier: Foreign Supplier, classification code must be one of: 010, 011, 033, 034, 035, 036, 037, 038, 039, 040, 041, 045.");
                            }
                        }

                        if (line.Taxes != null)
                        {
                            foreach (var tax in line.Taxes)
                            {
                                if (tax.TaxCategory == "E" && string.IsNullOrWhiteSpace(tax.TaxExemptionReason))
                                    tax.TaxExemptionReason = "Tax exempted as per applicable regulations";
                            }
                        }

                        line.CalculateAmounts();
                    }
                }

                // ================================
                // Validation Exit
                // ================================
                if (action != "saveDraft" && !ModelState.IsValid)
                {
                    var allErrors = ModelState
                        .SelectMany(kvp => kvp.Value?.Errors?.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>())
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();

                    if (isAjax)
                        return AjaxFail(allErrors.FirstOrDefault() ?? "Validation failed.", 400, allErrors);

                    Message = "Validation failed. Please correct the errors and try again.";
                    await OnGetAsync();
                    return Page();
                }

                // ================================
                // Build JSON Model
                // ================================
                var invoiceHeader = CreateInvoiceHeader(
                    supplier!,
                    customer,
                    publicCustomer);

                var invoiceJson = _invoiceMapper.MapToJsonModel(invoiceHeader);

                foreach (var line in invoiceHeader.InvoiceLines)
                    line.CalculateAmounts();

                // ================================
                // Action Switch
                // ================================
                switch (action)
                {
                    case "generateJson":
                        GeneratedJson = invoiceJson;
                        Message = "JSON generated successfully!";
                        if (isAjax)
                            return new JsonResult(new { success = true, message = Message, invoiceNo = Invoice!.InvoiceNo });
                        return Page();

                    case "saveDraft":
                        if (SaveDraft(invoiceJson, supplier!, customer, publicCustomer))
                        {
                            var wasUpdate = _context.InvoiceHeaders.Any(ih => ih.InvoiceNo == Invoice!.InvoiceNo);

                            return new JsonResult(new
                            {
                                success = true,
                                draftPath = HttpContext.Session.GetString("DraftFilePath"),
                                invoiceNo = Invoice!.InvoiceNo,
                                isUpdate = wasUpdate
                            });
                        }
                        return AjaxFail("Failed to save draft.", 500);

                    case "submitDocuments":
                        return await OnPostSubmitDocumentsAsync(Invoice!.InvoiceNo ?? string.Empty, true);

                    case "saveAndSubmit":
                        if (!SaveDraft(invoiceJson, supplier!, customer, publicCustomer))
                            return AjaxFail("Could not save draft. Submission aborted.", 500);

                        return await OnPostSubmitDocumentsAsync(Invoice!.InvoiceNo ?? string.Empty, true);

                    default:
                        return isAjax
                            ? AjaxFail("Unknown action.", 400)
                            : Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in OnPostAsync");

                if (isAjax)
                    return AjaxFail("An error occurred while processing your request. Please try again.", 500);

                Message = "An error occurred while processing your request. Please try again.";
                await OnGetAsync();
                return Page();
            }
        }


        private bool SaveDraft(
            string invoiceJson,
            PartyInfo supplier,
            PartyInfo? customer,
            PublicCustomer? publicCustomer)
        {
            try
            {
                // Step 1: Generate a unique invoice number
                if (string.IsNullOrEmpty(Invoice.InvoiceNo))
                {
                    Invoice.InvoiceNo = GenerateNextInvoiceNumber();
                }

                // Ensure VM totals exist before copying to DB header
                foreach (var line in Invoice.InvoiceLines)
                    line.CalculateAmounts();

                Invoice.CalculateInvoiceTotals();

                // Step 1.5: Check if this draft already exists
                var existingDraft = _context.InvoiceHeaders
                    .Include(ih => ih.InvoiceLines)
                    .FirstOrDefault(ih => ih.InvoiceNo == Invoice.InvoiceNo);

                bool isUpdate = existingDraft != null;
                _logger.LogDebug("SaveDraft - Invoice {InvoiceNo}: {Action}", Invoice.InvoiceNo, isUpdate ? "UPDATING existing draft" : "CREATING new draft");

                InvoiceHeader draftInvoice;

                if (isUpdate)
                {
                    // Update existing draft
                    draftInvoice = existingDraft!;

                    // Remove existing lines
                    _context.InvoiceLines.RemoveRange(draftInvoice.InvoiceLines);

                    // Update header fields
                    draftInvoice.RefDocumentNo = Invoice.RefDocumentNo;
                    draftInvoice.UUID = Invoice.UUID;
                    draftInvoice.ForeignCurrency = Invoice.Currency ?? "MYR";
                    draftInvoice.ExchangeRate = Invoice.ExchangeRate;
                    draftInvoice.Supplier = supplier!;

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

                    draftInvoice.UpdatedBy = User.Identity?.Name ?? "System";
                    draftInvoice.LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
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
                }
                else
                {
                    // Step 2: Create new invoice header
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
                        CustomerId = customer?.PartyInfoId,
                        PublicCustomer = publicCustomer,
                        PublicCustomerId = publicCustomer?.PublicCustomerId,
                        CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                        IssueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                        InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"),
                        CreatedBy = User.Identity?.Name ?? "System",
                        UpdatedBy = User.Identity?.Name ?? "System",
                        LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
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

                // Step 3: Add invoice lines (for both new and updated drafts)
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
                        InvoiceTaxes = lineView.Taxes?.Select(tax => new InvoiceTax
                        {
                            TaxCategory = tax.TaxCategory,
                            TaxPercentage = tax.TaxPercentage,
                            TaxAmount = tax.TaxAmount,
                            TaxExemptionReason = tax.TaxCategory == "E" && string.IsNullOrWhiteSpace(tax.TaxExemptionReason)
                                ? "Tax exempted as per applicable regulations"
                                : tax.TaxExemptionReason ?? ""
                        }).ToList() ?? new List<InvoiceTax>()
                    };

                    line.CalculateAmounts(); // ✅ Subtotal and tax computed
                    return line;
                }).ToList();

                // Assign the lines to the invoice
                if (isUpdate)
                {
                    draftInvoice.InvoiceLines = newLines?.ToList() ?? new List<InputModels.InvoiceLine>();
                }
                else
                {
                    draftInvoice.InvoiceLines = newLines?.ToList() ?? new List<InputModels.InvoiceLine>();
                    _context.InvoiceHeaders.Add(draftInvoice);
                }

                _context.SaveChanges();

                _invoiceHistoryService.Log(draftInvoice.InvoiceNo, isUpdate ? "Updated" : "Created",
                    isUpdate ? "Invoice draft updated" : "Invoice draft created");



                // Step 3: Save the JSON file to the drafts folder on another server
                var draftsFolder = _filePathConfig.DraftFolder;  // Directly from config (supports network path)

                _logger.LogInformation("Drafts folder path: {DraftsFolder}", draftsFolder);

                // Ensure the drafts folder exists
                if (!Directory.Exists(draftsFolder))
                {
                    _logger.LogInformation("Drafts folder does not exist. Creating it...");
                    Directory.CreateDirectory(draftsFolder);
                }

                // Generate the file path for the draft
                var fileName = $"{Invoice.InvoiceNo}.json";
                var filePath = Path.Combine(draftsFolder, fileName);

                _logger.LogInformation("Saving draft to: {FilePath}", filePath);

                // Write the JSON content to the file
                System.IO.File.WriteAllText(filePath, invoiceJson);

                // Save the draft path in session and view data
                HttpContext.Session.SetString("DraftFilePath", filePath);
                ViewData["DraftFilePath"] = filePath;

                _logger.LogInformation("Draft saved successfully at: {FilePath}", filePath);

                Message = $"Draft saved as {fileName}";

                // Clear the DraftFilePath from the session to avoid reloading the same draft
                //HttpContext.Session.Remove("DraftFilePath");
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
                _logger.LogInformation($"📄 JSON Document Content Preview: {invoiceJson.Substring(0, Math.Min(500, invoiceJson.Length))}...");
                var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
                var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

                var documents = new List<Documents>
                {
                    new Documents("JSON", documentHash, invoiceNo, encodedDocument)
                };

                // Get the correct TIN based on document type (self-billed uses customer TIN, regular uses supplier TIN)
                var fullInvoice = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

                if (fullInvoice == null)
                {
                    var msg = $"Invoice {invoiceNo} not found with supplier/customer data.";
                    _logger.LogError(msg);
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                // Determine TIN based on document type - self-billed (11,12,13,14) use customer TIN
                string? tin = fullInvoice.DocTypeCode switch
                {
                    "11" or "12" or "13" or "14" => fullInvoice.Customer?.TIN,
                    _ => fullInvoice.Supplier?.TIN
                };

                if (string.IsNullOrWhiteSpace(tin))
                {
                    var tinType = new[] { "11", "12", "13", "14" }.Contains(fullInvoice.DocTypeCode) ? "customer" : "supplier";
                    var msg = $"Missing {tinType} TIN for document type {fullInvoice.DocTypeCode}. Cannot submit to LHDN.";
                    _logger.LogError($"🚫 {msg} - InvoiceNo: {invoiceNo}, DocType: {fullInvoice.DocTypeCode}");
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                _logger.LogInformation($"🔑 Using TIN for submission - InvoiceNo: {invoiceNo}, DocType: {fullInvoice.DocTypeCode}, TIN: {tin}");
                _logger.LogInformation($"📊 Debug TIN Info - SupplierTIN: {fullInvoice.Supplier?.TIN ?? "NULL"}, CustomerTIN: {fullInvoice.Customer?.TIN ?? "NULL"}");

                // The submitting TIN is the invoice issuer (supplier, or customer for self-billed). Read
                // access (CanAccessInvoiceAsync) is granted to either party, so explicitly require the user
                // to OWN this issuer TIN before submitting under it — otherwise a counterparty who only owns
                // the other party's TIN could submit as the issuer.
                if (!await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(User, _context, tin))
                {
                    _logger.LogWarning("🚫 User not authorized to submit InvoiceNo {InvoiceNo} under issuer TIN {TIN}.", invoiceNo, tin);
                    var msg = "You are not authorized to submit this invoice.";
                    return isAjax ? new JsonResult(new { success = false, message = msg }) : Page();
                }

                var accessToken = await _tokenService.GetAccessTokenForTIN(tin);

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
                        invoiceDirection = "Sent"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Error] Exception during submission of Invoice: {invoiceNo}");
                return new JsonResult(new
                {
                    success = false,
                    message = $"Error submitting Invoice: {ex.Message}"
                });
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

                string? internalStatus = _statusMappingService.MapLhdnStatusToInternalStatus(lhdnStatus);

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
                Directory.CreateDirectory(directoryPath ?? "");
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

        private bool IsSelfBilled(string docTypeCode)
        {
            return new[] { "11", "12", "13", "14" }.Contains(docTypeCode);
        }

        private InputModels.InvoiceHeader CreateInvoiceHeader(
            PartyInfo supplier,
            PartyInfo? customer,
            InputModels.PublicCustomer? publicCustomer)
        {
            _logger.LogDebug("CreateInvoiceHeader: SupplierTIN={SupplierTIN}, CustomerTIN={CustomerTIN}, PublicCustomerId={PublicCustomerId}",
                supplier?.TIN ?? "NULL", customer?.TIN ?? "NULL", publicCustomer?.PublicCustomerId.ToString() ?? "NULL");

            var invoiceHeader = new InputModels.InvoiceHeader
            {
                InvoiceNo = Invoice.InvoiceNo ?? "",
                RefDocumentNo = Invoice.RefDocumentNo,
                IssueDate = Invoice.IssueDate,
                DocTypeCode = Invoice.DocTypeCode,
                InvoicePeriod = Invoice.InvoicePeriod,
                Currency = Invoice.Currency,
                ForeignCurrency = Invoice.ForeignCurrency ?? "MYR",
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

            // Always assign supplier
            invoiceHeader.Supplier = supplier!;

            if (customer != null)
            {
                invoiceHeader.Customer = customer;
                invoiceHeader.CustomerId = customer.PartyInfoId;
                _logger.LogDebug("CreateInvoiceHeader: Assigned PartyInfo Customer TIN={TIN}", customer.TIN);
            }
            else if (publicCustomer != null)
            {
                invoiceHeader.PublicCustomer = publicCustomer;
                invoiceHeader.PublicCustomerId = publicCustomer.PublicCustomerId;
                _logger.LogDebug("CreateInvoiceHeader: Assigned PublicCustomer ID={Id}", publicCustomer.PublicCustomerId);
            }

            _logger.LogDebug("CreateInvoiceHeader: DocType={DocType}, IsSelfBilled={IsSelfBilled}", Invoice.DocTypeCode, IsSelfBilled(Invoice.DocTypeCode));

            // Populate invoice totals
            invoiceHeader.TotalDiscountAmount = Invoice.TotalDiscountAmount;
            invoiceHeader.StartDate = Invoice.StartDate;
            invoiceHeader.EndDate = Invoice.EndDate;
            invoiceHeader.TotalAmountExclTax = Invoice.TotalAmountExclTax;
            invoiceHeader.TotalTaxAmount = Invoice.TotalTaxAmount;
            invoiceHeader.TotalAmountIncTax = Invoice.TotalAmountIncTax;
            invoiceHeader.TotalPayableAmount = Invoice.TotalPayableAmount;
            invoiceHeader.TotalNetAmount = Invoice.TotalNetAmount;

            // ✅ Populate and calculate Invoice Lines (UNCHANGED)
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
                        TaxExemptionReason =
                            tax.TaxCategory == "E" && string.IsNullOrWhiteSpace(tax.TaxExemptionReason)
                                ? "Tax exempted as per applicable regulations"
                                : tax.TaxExemptionReason ?? ""
                    }).ToList()
                };

                line.CalculateAmounts(); // Ensures Subtotal + Tax + Totals
                return line;
            }).ToList();

            return invoiceHeader;
        }

        private string WrapText(string text, int maxLineLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Normalize newlines
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var paragraphs = text.Split('\n');
            var wrappedLines = new List<string>();

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    wrappedLines.Add("");
                    continue;
                }

                var words = para.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var currentLine = "";

                foreach (var word in words)
                {
                    // Calculate length if we add a space plus the new word
                    int spaceLength = currentLine.Length > 0 ? 1 : 0;

                    if (currentLine.Length + spaceLength + word.Length > maxLineLength)
                    {
                        // Push current line and reset
                        if (currentLine.Length > 0)
                        {
                            wrappedLines.Add(currentLine);
                            currentLine = "";
                        }

                        // If a single word is longer than max length, force-break it
                        var chunkedWord = word;
                        while (chunkedWord.Length > maxLineLength)
                        {
                            wrappedLines.Add(chunkedWord.Substring(0, maxLineLength));
                            chunkedWord = chunkedWord.Substring(maxLineLength);
                        }
                        currentLine = chunkedWord;
                    }
                    else
                    {
                        // Append word to the current line safely
                        currentLine += (currentLine.Length > 0 ? " " : "") + word;
                    }
                }

                if (currentLine.Length > 0)
                {
                    wrappedLines.Add(currentLine);
                }
            }

            return string.Join("\n", wrappedLines);
        }

        private string GetInternalStatus(string invoiceId)
        {
            var submission = _context.InvoiceSubmissions.FirstOrDefault(s => s.InvoiceNo == invoiceId);
            return submission?.InternalStatus?.StatusCode ?? "Unknown";
        }

        private async Task<IActionResult> SaveAsTemplateAsync(string TemplateName)
        {
            _logger.LogDebug("SaveAsTemplateAsync - TemplateName: '{TemplateName}'", TemplateName);
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (Invoice == null)
                    Invoice = new InvoiceHeaderView();

                _logger.LogDebug("SaveAsTemplateAsync - DocTypeCode={DocType}, Currency={Currency}, Lines={Lines}",
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

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetLoadPartyDetailsAsync(int partyId, string partyType)
        {
            try
            {
                _logger.LogDebug("OnGetLoadPartyDetailsAsync: PartyId={PartyId}, Type={PartyType}", partyId, partyType);

                object? partyData = null;

                // 1. Fetch from PartyInfo
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
                            PaymentTerms = p.PaymentTerms,
                            Email = p.Email,
                            Address = p.Addr1 + " " + (p.Addr2 ?? "") + " " + (p.CityName ?? "")
                        })
                        .FirstOrDefaultAsync();
                }
                // 2. Fetch from PublicCustomer
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
                            PaymentTerms = p.PaymentTerms,
                            Email = p.Email,
                            Address = p.Addr1 + " " + (p.Addr2 ?? "") + " " + (p.CityName ?? "")
                        })
                        .FirstOrDefaultAsync();
                }

                if (partyData == null)
                {
                    _logger.LogDebug("Party not found for ID={PartyId}", partyId);
                    return new JsonResult(new { success = false, message = "Buyer details not found" });
                }

                return new JsonResult(new
                {
                    success = true,
                    data = partyData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading party details for ID: {PartyId} Type: {Type}", partyId, partyType);
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while loading party details"
                });
            }
        }


        [IgnoreAntiforgeryToken]
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

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetLoadCustomersAsync(int supplierId, string docTypeCode = "")
        {
            try
            {
                _logger.LogDebug("OnGetLoadCustomersAsync: SupplierId={SupplierId}, DocTypeCode='{DocTypeCode}'", supplierId, docTypeCode);

                if (supplierId <= 0)
                {
                    return new JsonResult(new List<object>());
                }

                bool isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(docTypeCode);

                if (isSelfBilled)
                {
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
                    var resultList = await _buyerService.GetCombinedBuyersBySupplierAsync(supplierId);
                    _logger.LogDebug("Buyer count: {Count}", resultList?.Count ?? 0);

                    // Return the list from the service directly
                    return new JsonResult(resultList ?? new List<SelectListItem>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers for supplierId: {SupplierId}", supplierId);
                return new JsonResult(new List<object>());
            }
        }

        [IgnoreAntiforgeryToken]
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

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetGetInvoicesForReferenceAsync(int supplierId)
        {
            try
            {
                _logger.LogInformation("🔍 GetInvoicesForReference called for supplierId: {supplierId}", supplierId);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // SECURITY: Get user's companies to ensure only their invoices are returned
                var userCompanies = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.PartyInfoId)
                    .ToListAsync();

                if (!userCompanies.Any())
                {
                    _logger.LogWarning("🔒 User {userId} has no associated companies - denying RefUUID access", userId);
                    return new JsonResult(new List<object>());
                }

                // SECURITY: Verify the requested supplier belongs to the current user
                if (!userCompanies.Contains(supplierId))
                {
                    _logger.LogWarning("🔒 Security violation: User {userId} attempted to access invoices for supplier {supplierId} which doesn't belong to them", userId, supplierId);
                    return new JsonResult(new List<object>());
                }

                // First, let's check how many invoices exist for this supplier (any status)
                var allInvoicesCount = await _context.InvoiceHeaders
                    .Where(i => i.SupplierId == supplierId)
                    .CountAsync();
                _logger.LogInformation("📊 Total invoices for supplier {supplierId}: {count}", supplierId, allInvoicesCount);

                // Check how many non-draft invoices exist
                var nonDraftCount = await _context.InvoiceHeaders
                    .Where(i => i.SupplierId == supplierId && i.InternalStatusId != "Draft")
                    .CountAsync();
                _logger.LogInformation("📊 Non-draft invoices for supplier {supplierId}: {count}", supplierId, nonDraftCount);

                // Check how many regular invoices (DocTypeCode = "01") exist
                var regularInvoicesCount = await _context.InvoiceHeaders
                    .Where(i => i.SupplierId == supplierId && i.DocTypeCode == "01")
                    .CountAsync();
                _logger.LogInformation("📊 Regular invoices (01) for supplier {supplierId}: {count}", supplierId, regularInvoicesCount);

                // Get invoices for the specified supplier that can be referenced by Credit Notes
                var invoices = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer)
                    .Include(i => i.PublicCustomer)
                    .Where(i => i.SupplierId == supplierId
                        && i.InternalStatusId != "Draft"  // Exclude draft invoices
                        && i.DocTypeCode == "01") // Only regular invoices can be referenced by Credit Notes
                    .Select(i => new {
                        uuid = i.UUID,
                        invoiceNo = i.InvoiceNo,
                        issueDate = i.IssueDate.HasValue ? i.IssueDate.Value.ToString("yyyy-MM-dd") : "",
                        supplierName = i.Supplier != null ? i.Supplier.CompanyName : "Unknown Supplier",
                        customerName = i.Customer != null ? i.Customer.CompanyName : "Unknown Customer",
                        status = i.InternalStatusId ?? "Unknown",
                        totalAmount = i.TotalAmountIncTax.HasValue ? i.TotalAmountIncTax.Value.ToString("F2") : "0.00",
                        docType = i.DocTypeCode
                    })
                    .OrderByDescending(i => i.invoiceNo)
                    .Take(50) // Limit to recent 50 invoices for performance
                    .ToListAsync();

                _logger.LogInformation("✅ Found {count} invoices for reference selection (supplier access verified)", invoices.Count);
                _logger.LogInformation("🔒 Security context: User {userId} with {companyCount} companies, accessing supplier {supplierId}",
                    userId, userCompanies.Count, supplierId);

                // Log first few invoices for debugging
                foreach (var inv in invoices.Take(3))
                {
                    _logger.LogInformation("📄 Invoice: {invoiceNo}, Supplier: {supplierName}, Customer: {customerName}, Status: {status}, UUID: {uuid}",
                        inv.invoiceNo, inv.supplierName, inv.customerName, inv.status, inv.uuid ?? "NULL");
                }

                return new JsonResult(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting invoices for reference with supplierId: {supplierId}", supplierId);
                return new JsonResult(new List<object>()); // Return empty array instead of BadRequest
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
