using CsvHelper;
using CsvHelper.Configuration;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Services.Extensions;
using eInvWorld.Services.Logging;
using eInvWorld.Services.Mappers;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EINVWORLD.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier")]
    public class ImportCSVModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly InvoiceMapper _invoiceMapper;
        private readonly IStatusMappingService _statusMappingService;
        private readonly InvoiceHistoryService _invoiceHistoryService;
        private readonly FilePathConfig _filePathConfig;
        private readonly ILogger<ImportCSVModel> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly eInvWorld.Services.InvoiceService _invoiceService;

        // Note: IWebHostEnvironment was added here to access wwwroot files!
        public ImportCSVModel(
            ApplicationDbContext context,
            IStatusMappingService statusMappingService,
            InvoiceHistoryService invoiceHistoryService,
            IOptions<FilePathConfig> filePathConfig,
            ILogger<ImportCSVModel> logger,
            IWebHostEnvironment env,
            eInvWorld.Services.InvoiceService invoiceService) : base(context)
        {
            _context = context;
            _invoiceMapper = new InvoiceMapper();
            _statusMappingService = statusMappingService;
            _invoiceHistoryService = invoiceHistoryService;
            _filePathConfig = filePathConfig.Value;
            _logger = logger;
            _env = env;
            _invoiceService = invoiceService;
        }

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        [BindProperty]
        public string? ValidRecordsJson { get; set; }

        public List<PreviewInvoiceModel> PreviewRecords { get; set; } = new();

        public IActionResult OnGet()
        {
            return Page();
        }

        // --- NEW: DYNAMIC JSON TO CSV DOWNLOADER ---
        public async Task<IActionResult> OnGetDownloadReferenceAsync(string type)
        {
            // Strict allowlist to prevent arbitrary file reading
            var validFiles = new[] { "ClassificationCodes", "CountryCodes", "CurrencyCodes", "EInvoiceTypes", "MSICSubCategoryCodes", "PaymentMethods", "StateCodes", "TaxTypes", "UnitTypes" };

            if (string.IsNullOrEmpty(type) || !validFiles.Contains(type))
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, "codes", $"{type}.json");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var sb = new StringBuilder();

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Parse the JSON array and flatten it into CSV columns and rows
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstObj = root[0];
                    if (firstObj.ValueKind == JsonValueKind.Object)
                    {
                        // 1. Get Headers dynamically from JSON properties
                        var props = firstObj.EnumerateObject().Select(p => p.Name).ToList();
                        sb.AppendLine(string.Join(",", props.Select(p => $"\"{p}\"")));

                        // 2. Build Rows dynamically
                        foreach (var item in root.EnumerateArray())
                        {
                            var row = new List<string>();
                            foreach (var prop in props)
                            {
                                var val = item.TryGetProperty(prop, out var p) ? p.ToString() : "";
                                row.Add($"\"{val.Replace("\"", "\"\"")}\""); // Escape quotes for CSV format
                            }
                            sb.AppendLine(string.Join(",", row));
                        }
                    }
                }
                else
                {
                    // Fallback just in case JSON is not an array format
                    sb.Append(json);
                }
            }
            catch
            {
                // If something goes wrong parsing, return as a simple txt file
                var rawBytes = Encoding.UTF8.GetBytes(json);
                return File(rawBytes, "text/plain", $"{type}.txt");
            }

            // Return the dynamically built Excel-friendly CSV!
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"{type}.csv");
        }

        // --- Parsing: accept CSV or XLSX, both mapped to the same InvoiceCsvDto column names ---
        private static List<InvoiceCsvDto> ParseUploadedRecords(IFormFile file)
        {
            var name = file.FileName ?? string.Empty;
            if (name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return ParseXlsxRecords(file);

            // CSV (default). Lenient: trims, ignores missing/extra columns.
            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                HeaderValidated = null,
                MissingFieldFound = null
            });
            return csv.GetRecords<InvoiceCsvDto>().ToList();
        }

        // Map an .xlsx (first worksheet, header row) onto InvoiceCsvDto by matching header text to property
        // name (case-insensitive) — so the Excel and CSV formats share one column schema.
        private static List<InvoiceCsvDto> ParseXlsxRecords(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var wb = new ClosedXML.Excel.XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var rows = ws.RangeUsed()?.RowsUsed().ToList();
            if (rows == null || rows.Count < 2) return new List<InvoiceCsvDto>();

            var header = rows[0];
            var colByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= header.CellCount(); c++)
            {
                var h = header.Cell(c).GetString().Trim();
                if (!string.IsNullOrEmpty(h) && !colByName.ContainsKey(h)) colByName[h] = c;
            }

            var props = typeof(InvoiceCsvDto).GetProperties();
            var records = new List<InvoiceCsvDto>();
            foreach (var row in rows.Skip(1))
            {
                var dto = new InvoiceCsvDto();
                var anyValue = false;
                foreach (var p in props)
                {
                    if (!colByName.TryGetValue(p.Name, out var c)) continue;
                    var raw = row.Cell(c).GetString().Trim();
                    if (string.IsNullOrEmpty(raw)) continue;
                    anyValue = true;
                    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    if (t == typeof(string)) p.SetValue(dto, raw);
                    else if (t == typeof(decimal) &&
                             decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dval))
                        p.SetValue(dto, dval);
                }
                if (anyValue) records.Add(dto);
            }
            return records;
        }

        // Downloadable XLSX template whose headers match the importer's columns (one example row).
        public IActionResult OnGetTemplate()
        {
            var props = typeof(InvoiceCsvDto).GetProperties();
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Invoices");
            for (int i = 0; i < props.Length; i++)
            {
                ws.Cell(1, i + 1).Value = props[i].Name;
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "bulk-invoice-import-template.xlsx");
        }

        // --- STEP 1: UPLOAD AND VALIDATE ---
        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (UploadFile == null || UploadFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV or Excel (.xlsx) file.");
                return Page();
            }

            var uploadName = UploadFile.FileName ?? string.Empty;
            if (!uploadName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                && !uploadName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only .csv and .xlsx files are supported.");
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompany = await _context.UserCompanies.Include(uc => uc.PartyInfo)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync(uc => uc.UserId == userId);

            if (userCompany == null)
            {
                ModelState.AddModelError("", "No active company profile found.");
                return Page();
            }

            int primaryCompanyId = userCompany.PartyInfoId;

            var validClassificationCodes = new HashSet<string>(await _context.ClassificationCodes.Select(x => x.Code).ToListAsync());
            var validUnitOfMeasures = new HashSet<string>(await _context.UnitTypes.Select(x => x.Code).ToListAsync());
            var validTaxCategories = new HashSet<string>(await _context.TaxTypes.Select(x => x.Code).ToListAsync());

            var knownParties = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => sb.SupplierId == primaryCompanyId && sb.BuyerId == p.PartyInfoId) || p.TIN == "EI00000000010" || p.TIN == "EI00000000030")
                .ToListAsync();

            var publicCustomers = await _context.PublicCustomers
                .Where(p => p.CreatedByCompanyId == primaryCompanyId)
                .ToListAsync();

            PreviewRecords = new List<PreviewInvoiceModel>();
            var validLinesToKeep = new List<InvoiceCsvDto>();

            List<InvoiceCsvDto> rawRecords;
            try
            {
                rawRecords = ParseUploadedRecords(UploadFile);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Could not read the file: " + ex.Message);
                return Page();
            }

            {
                var groupedInvoices = rawRecords.GroupBy(r => r.InvoiceNo).ToList();

                foreach (var group in groupedInvoices)
                {
                    var firstLine = group.First();
                    var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(firstLine.DocTypeCode);

                    var previewRecord = new PreviewInvoiceModel
                    {
                        InvoiceNo = firstLine.InvoiceNo,
                        PoDoNo = firstLine.PoDoNo,
                        CounterpartyTIN = firstLine.CounterpartyTIN,
                        LineCount = group.Count(),
                        DocTypeCode = firstLine.DocTypeCode ?? "01",
                        Currency = string.IsNullOrWhiteSpace(firstLine.Currency) ? "MYR" : firstLine.Currency,
                        Errors = new List<string>()
                    };

                    if (!DateTime.TryParse(firstLine.IssueDate, out DateTime parsedDate))
                        previewRecord.Errors.Add($"Invalid IssueDate format: '{firstLine.IssueDate}'. Use yyyy-MM-dd.");
                    else
                        previewRecord.IssueDate = parsedDate;

                    if (!string.IsNullOrWhiteSpace(firstLine.StartDate) && !DateTime.TryParse(firstLine.StartDate, out _))
                        previewRecord.Errors.Add($"Invalid StartDate format: '{firstLine.StartDate}'. Use yyyy-MM-dd.");

                    if (!string.IsNullOrWhiteSpace(firstLine.EndDate) && !DateTime.TryParse(firstLine.EndDate, out _))
                        previewRecord.Errors.Add($"Invalid EndDate format: '{firstLine.EndDate}'. Use yyyy-MM-dd.");

                    var counterparty = knownParties.FirstOrDefault(p => p.TIN == firstLine.CounterpartyTIN);
                    var publicCounterparty = publicCustomers.FirstOrDefault(p => p.TIN == firstLine.CounterpartyTIN);

                    // Check if the TIN belongs to the user's own company profile
                    bool isOwnCompany = (firstLine.CounterpartyTIN == userCompany.PartyInfo?.TIN);

                    // Allow it to pass if it's their own company, even if they aren't assigned as a buyer
                    if (counterparty == null && publicCounterparty == null && !isOwnCompany)
                    {
                        previewRecord.Errors.Add($"TIN '{firstLine.CounterpartyTIN}' is not a registered buyer/supplier for your account.");
                    }

                    decimal totalExclTax = 0, totalTax = 0;
                    int lineIndex = 1;

                    foreach (var line in group)
                    {
                        if (line.ClassificationCode == null || !validClassificationCodes.Contains(line.ClassificationCode))
                            previewRecord.Errors.Add($"Line {lineIndex}: Invalid Classification Code.");

                        if (line.UnitOfMeasure == null || !validUnitOfMeasures.Contains(line.UnitOfMeasure))
                            previewRecord.Errors.Add($"Line {lineIndex}: Invalid Unit of Measure.");

                        if (line.TaxCategory == null || !validTaxCategories.Contains(line.TaxCategory))
                            previewRecord.Errors.Add($"Line {lineIndex}: Invalid Tax Category.");

                        if (isSelfBilled && counterparty != null)
                        {
                            if (counterparty.TIN == "EI00000000010" && line.ClassificationCode != "004")
                                previewRecord.Errors.Add($"Line {lineIndex}: Self-Billed to General Public MUST use Classification Code 004.");

                            var foreignCodes = new[] { "010", "011", "033", "034", "035", "036", "037", "038", "039", "040", "041", "045" };
                            if (counterparty.TIN == "EI00000000030" && !foreignCodes.Contains(line.ClassificationCode))
                                previewRecord.Errors.Add($"Line {lineIndex}: Invalid Classification Code for Foreign Supplier.");
                        }

                        // Handle nullable decimals properly
                        decimal qty = line.Quantity ?? 0;
                        decimal price = line.UnitPrice ?? 0;
                        decimal discount = line.DiscountAmount ?? 0;
                        decimal taxPct = line.TaxPercentage ?? 0;

                        var lineSubtotal = (qty * price) - discount;
                        totalExclTax += lineSubtotal;
                        totalTax += lineSubtotal * (taxPct / 100);
                        lineIndex++;
                    }

                    previewRecord.TotalAmount = totalExclTax + totalTax;
                    previewRecord.IsValid = !previewRecord.Errors.Any();

                    if (previewRecord.IsValid) validLinesToKeep.AddRange(group);

                    PreviewRecords.Add(previewRecord);
                }
            }

            ValidRecordsJson = JsonSerializer.Serialize(validLinesToKeep);
            return Page();
        }

        // --- STEP 2: CONFIRM, GENERATE JSON, AND SAVE DRAFTS ---
        public async Task<IActionResult> OnPostConfirmAsync()
        {
            if (string.IsNullOrWhiteSpace(ValidRecordsJson)) return RedirectToPage("./InvoiceLists");
            var records = JsonSerializer.Deserialize<List<InvoiceCsvDto>>(ValidRecordsJson);
            if (records == null || !records.Any()) return RedirectToPage("./InvoiceLists");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCompany = await _context.UserCompanies.Include(uc => uc.PartyInfo)
                .OrderByDescending(uc => uc.IsPrimaryCompany)
                .FirstOrDefaultAsync(uc => uc.UserId == userId);

            int successCount = 0;
            var draftsFolder = _filePathConfig.DraftFolder;
            if (!Directory.Exists(draftsFolder)) Directory.CreateDirectory(draftsFolder);

            foreach (var group in records.GroupBy(r => r.InvoiceNo))
            {
                var firstLine = group.First();
                var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(firstLine.DocTypeCode);

                PartyInfo? supplier = null;
                PartyInfo? customer = null;
                PublicCustomer? publicCustomer = null;

                var counterpartyParty = await _context.PartyInfos.FirstOrDefaultAsync(p => p.TIN == firstLine.CounterpartyTIN);
                var counterpartyPublic = await _context.PublicCustomers.FirstOrDefaultAsync(p => p.TIN == firstLine.CounterpartyTIN && p.CreatedByCompanyId == (userCompany != null ? userCompany.PartyInfoId : 0));

                if (isSelfBilled)
                {
                    customer = userCompany?.PartyInfo;
                    if (counterpartyParty != null) supplier = counterpartyParty;
                }
                else
                {
                    supplier = userCompany?.PartyInfo;
                    if (counterpartyParty != null) customer = counterpartyParty;
                    else if (counterpartyPublic != null) publicCustomer = counterpartyPublic;
                }

                InvoicePeriodEnum parsedPeriod = InvoicePeriodEnum.Not_Applicable;
                if (!string.IsNullOrWhiteSpace(firstLine.BillingFrequency))
                {
                    Enum.TryParse(firstLine.BillingFrequency.Replace(" ", "_"), true, out parsedPeriod);
                }

                DateTime? startDate = null;
                DateTime? endDate = null;
                if (!string.IsNullOrWhiteSpace(firstLine.StartDate) && DateTime.TryParse(firstLine.StartDate, out DateTime sDate)) startDate = sDate;
                if (!string.IsNullOrWhiteSpace(firstLine.EndDate) && DateTime.TryParse(firstLine.EndDate, out DateTime eDate)) endDate = eDate;

                var systemInvoiceNo = GenerateNextInvoiceNumber();

                var invoiceHeader = new InvoiceHeader
                {
                    InvoiceNo = systemInvoiceNo,
                    PrefixedID = systemInvoiceNo,
                    RefDocumentNo = firstLine.InvoiceNo,
                    PoDoNo = firstLine.PoDoNo,
                    DocTypeCode = firstLine.DocTypeCode ?? "01",
                    IssueDate = DateTime.Parse(firstLine.IssueDate ?? ""),
                    CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    LastUpdated = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    CreatedBy = User.Identity?.Name ?? "System",
                    UpdatedBy = User.Identity?.Name ?? "System",
                    InternalStatusId = _statusMappingService.GetStatusIdByCode("Draft"),
                    Currency = string.IsNullOrWhiteSpace(firstLine.Currency) ? "MYR" : firstLine.Currency,
                    ForeignCurrency = string.IsNullOrWhiteSpace(firstLine.Currency) ? "MYR" : firstLine.Currency,
                    ExchangeRate = firstLine.ExchangeRate > 0 ? firstLine.ExchangeRate.Value : 1,
                    InvoicePeriod = parsedPeriod,
                    StartDate = startDate,
                    EndDate = endDate,
                    BankAccountNo = firstLine.BankAccountNumber,
                    BankName = firstLine.BankName,
                    Attention = firstLine.AttentionTo,
                    PaymentTerms = firstLine.PaymentTerms,
                    Supplier = supplier!,
                    SupplierId = supplier?.PartyInfoId,
                    Customer = customer!,
                    CustomerId = customer?.PartyInfoId,
                    PublicCustomer = publicCustomer,
                    PublicCustomerId = publicCustomer?.PublicCustomerId,
                    InvoiceLines = new List<InvoiceLine>()
                };

                int lineNum = 1;
                foreach (var line in group)
                {
                    var invoiceLine = new InvoiceLine
                    {
                        LineNumber = lineNum++,
                        ItemCode = "",
                        ItemDescription = line.ItemDescription ?? "",

                        // Handle nullable decimals properly with fallbacks
                        Quantity = line.Quantity ?? 0,
                        UnitPrice = line.UnitPrice ?? 0,
                        DiscountAmount = line.DiscountAmount ?? 0,

                        UnitOfMeasure = line.UnitOfMeasure ?? "",
                        ClassificationCode = string.IsNullOrWhiteSpace(line.ClassificationCode) ? (isSelfBilled ? "004" : "022") : line.ClassificationCode,
                        InvoiceHeader = invoiceHeader,
                        InvoiceTaxes = new List<InvoiceTax>
                        {
                            new InvoiceTax
                            {
                                TaxCategory = line.TaxCategory ?? "",
                                
                                // Handle nullable decimal properly with fallback
                                TaxPercentage = line.TaxPercentage ?? 0,

                                TaxExemptionReason = line.TaxCategory == "E" ? "Tax exempted as per applicable regulations" : ""
                            }
                        }
                    };
                    invoiceLine.CalculateAmounts();
                    invoiceHeader.InvoiceLines.Add(invoiceLine);
                }

                invoiceHeader.TotalAmountExclTax = invoiceHeader.InvoiceLines.Sum(l => l.AmountExclTax ?? 0);
                invoiceHeader.TotalTaxAmount = invoiceHeader.InvoiceLines.Sum(l => l.InvoiceTaxes?.Sum(t => t.TaxAmount ?? 0) ?? 0);
                invoiceHeader.TotalAmountIncTax = invoiceHeader.TotalAmountExclTax + invoiceHeader.TotalTaxAmount;
                invoiceHeader.TotalDiscountAmount = invoiceHeader.InvoiceLines.Sum(l => l.DiscountAmount ?? 0);
                invoiceHeader.TotalPayableAmount = invoiceHeader.TotalAmountIncTax;
                invoiceHeader.TotalNetAmount = invoiceHeader.TotalAmountIncTax;

                _context.InvoiceHeaders.Add(invoiceHeader);
                await _context.SaveChangesAsync();

                var invoiceJson = _invoiceMapper.MapToJsonModel(invoiceHeader);
                var filePath = Path.Combine(draftsFolder, $"{systemInvoiceNo}.json");
                await System.IO.File.WriteAllTextAsync(filePath, invoiceJson);

                _invoiceHistoryService.Log(systemInvoiceNo, "Created", $"Invoice draft created via CSV Import (Ref: {firstLine.InvoiceNo})");

                successCount++;
            }

            TempData["SuccessMessage"] = $"Successfully imported {successCount} invoices as Drafts!";
            return RedirectToPage("InvoiceLists");
        }

        // Consolidated into InvoiceService. Each imported draft is saved before the next call (see loop),
        // so the shared generator sees the incremented max — behavior preserved.
        private string GenerateNextInvoiceNumber() => _invoiceService.GenerateNextInvoiceNumber();

        // --- DTOs ---
        public class PreviewInvoiceModel
        {
            public string? InvoiceNo { get; set; }
            public string? PoDoNo { get; set; }
            public string? CounterpartyTIN { get; set; }
            public string? DocTypeCode { get; set; }
            public DateTime IssueDate { get; set; }
            public int LineCount { get; set; }
            public decimal TotalAmount { get; set; }
            public string? Currency { get; set; }
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        public class InvoiceCsvDto
        {
            [Required(ErrorMessage = "InvoiceNo is required")]
            public string? InvoiceNo { get; set; }

            [Required(ErrorMessage = "IssueDate is required")]
            public string? IssueDate { get; set; }

            public string? PoDoNo { get; set; }
            public string? DocTypeCode { get; set; }
            public string? Currency { get; set; }

            public decimal? ExchangeRate { get; set; }
            public string? BillingFrequency { get; set; }
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? BankAccountNumber { get; set; }
            public string? BankName { get; set; }
            public string? AttentionTo { get; set; }
            public string? PaymentTerms { get; set; }

            [Required(ErrorMessage = "CounterpartyTIN is required")]
            public string? CounterpartyTIN { get; set; }

            [Required(ErrorMessage = "ItemDescription is required")]
            public string? ItemDescription { get; set; }

            // Converted to nullable to prevent TypeConverterException
            public decimal? Quantity { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? DiscountAmount { get; set; }

            [Required(ErrorMessage = "UnitOfMeasure is required")]
            public string? UnitOfMeasure { get; set; }

            [Required(ErrorMessage = "ClassificationCode is required")]
            public string? ClassificationCode { get; set; }

            [Required(ErrorMessage = "TaxCategory is required")]
            public string? TaxCategory { get; set; }

            // Converted to nullable to prevent TypeConverterException
            public decimal? TaxPercentage { get; set; }
        }
    }
}