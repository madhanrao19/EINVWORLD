using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using eInvWorld.Models.Background;
using eInvWorld.Services;
using eInvWorld.Services.Logging;
using eInvWorld.Services.Mappers;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Documents = eInvWorld.Models.JsonModels.Documents;



namespace eInvWorld.Pages.Invoices
{
    [Authorize(Roles = "Admin,Supplier,Buyer")]
    public class InvoiceListsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILHDNApiService _lhdnApiService;
        private readonly ILogger<InvoiceListsModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEInvoiceNotificationService _eInvoiceEmailService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IJsonFileService _jsonFileService;
        private readonly InvoiceHistoryService _invoiceHistoryService;
        private readonly ITokenService _tokenService;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly InvoiceSyncHelper _invoiceSyncHelper;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ISyncJobTracker _jobTracker;

        public List<InvoiceHeader> DraftInvoices { get; set; } = new List<InvoiceHeader>();
        public List<InvoiceHeader> Invoices { get; set; } = new List<InvoiceHeader>();
        public List<InvoiceHeader> SubmittedInvoices { get; set; } = new List<InvoiceHeader>();
        public List<InvoiceHeader> ValidInvoices { get; set; } = new List<InvoiceHeader>();
        public List<InvoiceHeader> InvalidInvoices { get; set; } = new List<InvoiceHeader>();
        public List<InvoiceHeader> CancelledInvoices { get; set; } = new List<InvoiceHeader>();
        public List<DocumentSummary> RejectInvoices { get; set; } = new List<DocumentSummary>();

        public Metadata Metadata { get; set; } = null!;
        public SearchDocumentInput SearchInput { get; set; } = null!;
        public Dictionary<string, int> InvoiceSummaryByStatus { get; set; } = new();
        public int TotalSentInvoices { get; set; }
        public int TotalReceivedInvoices { get; set; }
        public string invoiceDirection { get; set; } = "Sent"; //  Set Default Value

        public string UserType { get; set; } = null!;
        public bool IsViewOnly { get; set; } = false;
        public List<string> UserTINs { get; set; } = new();
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }

        public string InvoiceDirection => UserType == "Buyer" ? "Received" : Request.Query["invoiceDirection"].ToString() ?? "Sent";

        private static readonly List<string> AllStatuses = new() { "Draft", "Valid", "Submitted", "Cancelled", "Invalid" };

        public InvoiceListsModel(ApplicationDbContext context,
                                 IConfiguration configuration,
                                 ILHDNApiService apiService,
                                 ILogger<InvoiceListsModel> logger,
                                 UserManager<ApplicationUser> userManager,
                                 IEInvoiceNotificationService eInvoiceEmailService,
                                 IServiceScopeFactory serviceScopeFactory,
                                 InvoiceHistoryService invoiceHistoryService,
                                 IJsonFileService jsonFileService,
                                 ITokenService tokenService,
                                 IPdfGeneratorService pdfGeneratorService,
                                 IHttpClientFactory httpClientFactory,
                                 InvoiceSyncHelper invoiceSyncHelper,
                                 IBackgroundTaskQueue taskQueue,
                                 ISyncJobTracker jobTracker)
        {
            _context = context;
            _configuration = configuration;
            _lhdnApiService = apiService;
            _logger = logger;
            _userManager = userManager;
            _eInvoiceEmailService = eInvoiceEmailService;
            _serviceScopeFactory = serviceScopeFactory;
            _invoiceHistoryService = invoiceHistoryService;
            _jsonFileService = jsonFileService;
            _tokenService = tokenService;
            _pdfGeneratorService = pdfGeneratorService;
            _httpClientFactory = httpClientFactory;
            _invoiceSyncHelper = invoiceSyncHelper;
            _taskQueue = taskQueue;
            _jobTracker = jobTracker;
        }
        public async Task<IActionResult> OnGetValidationDetailsAsync(string uuid, string submissionId, string tin, string invoiceNo)
        {
            try
            {
                if (string.IsNullOrEmpty(uuid) && string.IsNullOrEmpty(submissionId))
                    return new JsonResult(new { success = false, message = "UUID and Submission ID are missing." });

                //  1. CHECK DATABASE FIRST (Fast Load)
                var invoice = await _context.InvoiceHeaders.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo || i.UUID == uuid);
                if (invoice != null && !string.IsNullOrEmpty(invoice.LHDNValidationErrorJson))
                {
                    // 🔥 Return the raw JSON string directly to prevent the empty object [{}] bug!
                    string rawJson = $"{{\"success\": true, \"errors\": {invoice.LHDNValidationErrorJson}}}";
                    return Content(rawJson, "application/json");
                }

                // 2. IF NOT IN DB, PROCEED WITH LHDN API CALL
                if (string.IsNullOrEmpty(tin))
                    return new JsonResult(new { success = false, message = "TIN is missing." });

                var accessToken = await _tokenService.GetAccessTokenForTIN(tin);
                if (string.IsNullOrEmpty(accessToken))
                    return new JsonResult(new { success = false, message = "Authentication failed. Token could not be generated." });

                string baseUrl = _configuration["LHDNApiConfig:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string responseContent = "";
                List<object> errors = new List<object>();

                // Try Document Details endpoint first
                if (!string.IsNullOrEmpty(uuid))
                {
                    var requestUrl = $"{baseUrl}/api/v1.0/documents/{uuid}/details";
                    var response = await client.GetAsync(requestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        responseContent = await response.Content.ReadAsStringAsync();
                        var docValidation = JsonConvert.DeserializeObject<DocumentValidation>(responseContent);
                        errors = ExtractErrors(docValidation?.validationResults);
                    }
                }

                // Fallback: Try Submission endpoint
                if (!errors.Any() && !string.IsNullOrEmpty(submissionId))
                {
                    var requestUrl = $"{baseUrl}/api/v1.0/documentsubmissions/{submissionId}";
                    var response = await client.GetAsync(requestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        responseContent = await response.Content.ReadAsStringAsync();
                        var submission = JsonConvert.DeserializeObject<Submission>(responseContent);
                        var docSummary = submission?.documentSummary?.FirstOrDefault(d => d.uuid == uuid || string.IsNullOrEmpty(uuid));

                        if (docSummary != null && !string.IsNullOrEmpty(docSummary.documentStatusReason))
                        {
                            errors = new List<object>
                    {
                        new {
                            errorCode = "Submission Error",
                            error = docSummary.documentStatusReason,
                            propertyPath = ""
                        }
                    };
                        }
                    }
                }

                //  3. SAVE ERRORS TO DATABASE FOR NEXT TIME
                if (errors.Any())
                {
                    if (invoice != null)
                    {
                        invoice.LHDNValidationErrorJson = JsonConvert.SerializeObject(errors);
                        _context.InvoiceHeaders.Update(invoice);
                        await _context.SaveChangesAsync();
                    }
                    return new JsonResult(new { success = true, errors = errors });
                }

                return new JsonResult(new { success = true, errors = new List<object>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching validation details for UUID: {UUID}", uuid);
                return new JsonResult(new { success = false, message = "An internal error occurred." });
            }
        }

        private List<object> ExtractErrors(ValidationResults? results)
        {
            var errorList = new List<object>();
            if (results?.validationSteps != null)
            {
                foreach (var step in results.validationSteps.Where(s => s.status == "Invalid" || s.status == "Error"))
                {
                    if (step.error != null)
                    {
                        errorList.Add(new
                        {
                            errorCode = step.error.errorCode,
                            error = step.error.error,
                            propertyPath = step.error.propertyPath,
                            innerErrors = step.error.innerError?.Select(ie => new {
                                propertyName = ie.propertyName,
                                error = ie.error
                            }).ToList()
                        });
                    }
                }
            }
            return errorList;
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync(string invoiceNo)
        {
            try
            {
                if (string.IsNullOrEmpty(invoiceNo))
                {
                    _logger.LogError("Invoice number is required for PDF download");
                    return BadRequest("Invoice number is required");
                }

                _logger.LogInformation($"Generating PDF for invoice: {invoiceNo}");
                
                // Generate PDF using the latest PdfTemplate_v2 template
                string pdfPath = await _pdfGeneratorService.GeneratePdfAsync(invoiceNo);

                if (!System.IO.File.Exists(pdfPath))
                {
                    _logger.LogError($"PDF file not found at path: {pdfPath}");
                    return NotFound("PDF file could not be generated");
                }

                // Read the PDF file and return as download
                var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfPath);
                
                _logger.LogInformation($" PDF download successful for invoice: {invoiceNo}");
                
                return File(pdfBytes, "application/pdf", $"Invoice_{invoiceNo}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for invoice {invoiceNo}");
                TempData["ErrorMessage"] = "Failed to generate PDF. Please try again.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnGetAsync(int? pageNo, int? pageSize, string status, DateTime? submissionDateFrom, DateTime? submissionDateTo,
                            string invoiceDirection, string? searchQuery = null, string? documentType = null, bool refresh = false,
                            string? lhdnStatus = null, string? internalStatus = null, string? sortBy = null, string? sortOrder = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }
            var primaryCompany = await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.IsPrimaryCompany);
            var anyCompany = primaryCompany ?? await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.UserId == user.Id);
            if (anyCompany != null)
            {
                IsViewOnly = anyCompany.IsViewOnly;
            }
            UserType = user.UserType;
            this.SortBy = sortBy;
            this.SortOrder = sortOrder;

            // ENFORCE: If the user is a Buyer, strictly override any URL parameter
            if (UserType == "Buyer")
            {
                invoiceDirection = "Received";
            }
            // For Suppliers/Admins, set default to "Sent" if no parameter is provided
            else if (string.IsNullOrEmpty(invoiceDirection))
            {
                invoiceDirection = "Sent";
            }

            this.invoiceDirection = invoiceDirection;

            var docTypes = await _context.EInvoiceTypes.Where(dt => dt.IsActive).ToListAsync();
            ViewData["DocType"] = docTypes;

            if (submissionDateFrom == null && submissionDateTo == null)
            {
                submissionDateFrom = new DateTime(2024, 1, 1);
                submissionDateTo = DateTime.Now;
            }

            var fromUtc = submissionDateFrom!.Value.ToUniversalTime();
            var toUtc = submissionDateTo!.Value.ToUniversalTime();

            var cleanFromDate = new DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day,
                                             fromUtc.Hour, fromUtc.Minute, fromUtc.Second,
                                             DateTimeKind.Utc);

            var cleanToDate = new DateTime(toUtc.Year, toUtc.Month, toUtc.Day,
                                           toUtc.Hour, toUtc.Minute, toUtc.Second,
                                           DateTimeKind.Utc);

            SearchInput = new SearchDocumentInput
            {
                submissionDateFrom = cleanFromDate,
                submissionDateTo = cleanToDate,
                pageNo = pageNo ?? 1,
                pageSize = pageSize ?? 25,
                invoiceDirection = invoiceDirection,
                status = status,
                documentType = documentType ?? "",
                searchQuery = searchQuery ?? "",
                lhdnStatus = lhdnStatus ?? "",
                internalStatus = invoiceDirection == "Draft" ? "Draft" : internalStatus ?? "",
                sortBy = sortBy ?? "InvoiceNo",
                sortOrder = sortOrder ?? "desc"
            };

            _logger.LogInformation($"Search Input: {JsonConvert.SerializeObject(SearchInput)}");

            try
            {
                // 🚀 TRIGGER API REFRESH FIRST — backgrounded so the page returns immediately.
                // The actual LHDN import runs on the shared queue (paced by LhdnRateLimitHandler),
                // so clicking "Refresh from API" no longer blocks the request or risks a 429 storm.
                if (refresh)
                {
                    _logger.LogInformation("Forced refresh triggered for Invoice List by user {UserId}.", user.Id);

                    // 1. Cooldown check (5 min) — keep users from spamming LHDN.
                    var lastRefresh = HttpContext.Session.GetString("LastLHDNSync");
                    if (!string.IsNullOrEmpty(lastRefresh) &&
                        DateTime.TryParse(lastRefresh, out var lastTime) &&
                        (DateTime.UtcNow - lastTime).TotalMinutes < 5)
                    {
                        var remaining = Math.Max(1, (int)Math.Ceiling(5 - (DateTime.UtcNow - lastTime).TotalMinutes));
                        TempData["Message"] = $"⏳ Refresh was run recently. Please wait about {remaining} more minute(s) before refreshing again.";
                        return RedirectToPage("./InvoiceLists", new { invoiceDirection = this.invoiceDirection });
                    }

                    // 2. Resolve this user's own company TINs (exclude General TINs — they can't request a token).
                    var allUserTins = await _context.UserCompanies
                        .Where(uc => uc.UserId == user.Id)
                        .Include(uc => uc.PartyInfo)
                        .Select(uc => uc.PartyInfo.TIN)
                        .Distinct()
                        .ToListAsync();

                    var refreshTins = allUserTins.Where(tin => !GeneralTINHelper.IsGeneralTIN(tin)).ToList();

                    if (!refreshTins.Any())
                    {
                        TempData["Message"] = "❌ No valid company TIN linked to your account for an LHDN refresh.";
                        return RedirectToPage("./InvoiceLists", new { invoiceDirection = this.invoiceDirection });
                    }

                    HttpContext.Session.SetString("LastLHDNSync", DateTime.UtcNow.ToString("o"));

                    string refreshUserName = User?.Identity?.Name ?? "System";

                    // 3. Enqueue one paced background import per TIN; the request returns right away.
                    foreach (var tin in refreshTins)
                    {
                        var capturedTin = tin; // avoid closure capture of the loop variable
                        var jobId = await _jobTracker.CreateAsync(capturedTin, SyncJobType.SupplierRefresh, refreshUserName);

                        await _taskQueue.EnqueueAsync(capturedTin, async token =>
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var tracker = scope.ServiceProvider.GetRequiredService<ISyncJobTracker>();
                            await tracker.MarkRunningAsync(jobId);
                            try
                            {
                                var syncHelper = scope.ServiceProvider.GetRequiredService<InvoiceSyncHelper>();
                                // Supplier-initiated refresh is capped to a short 7-day lookback window.
                                var result = await syncHelper.RunFullImportFromLhdnAsync(capturedTin, refreshUserName, lookbackDays: 7);
                                _logger.LogInformation("[Refresh] TIN {Tin}: {Result}", capturedTin, result);
                                await tracker.MarkCompletedAsync(jobId, result);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[Refresh] TIN {Tin} job {JobId} failed", capturedTin, jobId);
                                await tracker.MarkFailedAsync(jobId, ex.Message);
                            }
                        });
                    }

                    TempData["Message"] = $"✅ Refresh started for {refreshTins.Count} company(ies). It runs in the background — reload this list in a moment to see the latest invoices.";

                    // Redirect to clear the ?refresh=true from the URL and clean up the address bar
                    return RedirectToPage("./InvoiceLists", new
                    {
                        invoiceDirection = this.invoiceDirection
                    });
                }

                // Now build the safe database query
                var query = _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer).Include(i => i.PublicCustomer)
                    .OrderByDescending(i => i.InvoiceNo)
                    .AsQueryable();

                var userTINs = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .Distinct()
                    .ToListAsync();

                UserTINs = userTINs;

                int currentPage = Math.Max(1, pageNo ?? 1);
                int pageSizeValue = Math.Max(1, pageSize ?? 25);

                //  Apply invoice direction logic dynamically (FIXED LOGIC)
                var selfBilledTypes = new[] { "11", "12", "13", "14" };

                if (invoiceDirection == "Draft")
                {
                    query = query.Where(i =>
                        i.InternalStatusId == "Draft" &&
                        (
                            (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN)) ||
                            (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN))
                        )
                    );
                }
                else if (invoiceDirection == "Sent")
                {
                    query = query.Where(i =>
                        i.InternalStatusId != "Draft" &&
                        (
                            (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN)) ||
                            (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN))
                        )
                    );
                }
                else if (invoiceDirection == "Received")
                {
                    query = query.Where(i =>
                        i.InternalStatusId != "Draft" &&
                        i.InternalStatusId != "Invalid" &&
                        i.LHDNStatusId != "Invalid" &&
                        !string.IsNullOrEmpty(i.UUID) &&
                        (
                            (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN)) ||
                            (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN))
                        )
                    );
                }

                //  Apply additional filters
                if (!string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(documentType) ||
                    !string.IsNullOrEmpty(searchQuery) || !string.IsNullOrEmpty(lhdnStatus) ||
                    !string.IsNullOrEmpty(internalStatus) || submissionDateFrom.HasValue || submissionDateTo.HasValue
                    || !string.IsNullOrEmpty(sortBy) || !string.IsNullOrEmpty(sortOrder))
                {
                    query = ApplyFilters(query, submissionDateFrom, submissionDateTo, status, documentType, searchQuery,
                        lhdnStatus, internalStatus, sortBy, sortOrder);
                }

                int totalInvoices = await query.CountAsync();

                //  Apply pagination
                query = query.Skip((currentPage - 1) * pageSizeValue)
                             .Take(pageSizeValue);

                Invoices = await query.ToListAsync();

                Metadata = new Metadata
                {
                    totalCount = totalInvoices,
                    totalPages = (int)Math.Ceiling((double)totalInvoices / pageSizeValue)
                };

                _logger.LogInformation($"Fetched {Invoices?.Count ?? 0} invoices after pagination.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoices");
                Invoices = new List<InvoiceHeader>();
            }
            return Page();
        }

        // NOTE: The old synchronous RefreshInvoicesFromApi (+ its GetAccessTokenWithRetry /
        // SearchDocumentsWithRetry helpers) was removed. The "Refresh from API" button now enqueues
        // a paced background import (see the refresh branch in OnGet) instead of blocking the request.

        private IQueryable<InvoiceHeader> ApplyFilters(
            IQueryable<InvoiceHeader> query,
            DateTime? submissionDateFrom,
            DateTime? submissionDateTo,
            string? status,
            string? documentType,
            string? searchQuery,
            string? lhdnStatus,
            string? internalStatus,
            string? sortBy,
            string? sortOrder)
        {
            // Apply existing filters
            if (submissionDateFrom.HasValue && submissionDateTo.HasValue)
            {
                query = query.Where(i => i.IssueDate >= submissionDateFrom.Value && i.IssueDate <= submissionDateTo.Value);
            }

            if (!string.IsNullOrEmpty(lhdnStatus))
            {
                query = query.Where(i => i.LHDNStatusId == lhdnStatus);
            }

            if (!string.IsNullOrEmpty(internalStatus))
            {
                query = query.Where(i => i.InternalStatusId == internalStatus);
            }

            if (!string.IsNullOrEmpty(documentType))
            {
                query = query.Where(i => i.DocTypeCode == documentType);
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(i => EF.Functions.Like(i.InvoiceNo, $"%{searchQuery}%")
                                      || EF.Functions.Like(i.RefDocumentNo, $"%{searchQuery}%")
                                      || EF.Functions.Like(i.Supplier.CompanyName, $"%{searchQuery}%")
                                      || EF.Functions.Like(i.Customer != null
                                                                ? i.Customer.CompanyName
                                                                : i.PublicCustomer!.CompanyName,
                                                            $"%{searchQuery}%"
                                                        )
                                                        );
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(sortBy))
            {
                bool isDescending = !string.IsNullOrEmpty(sortOrder) && sortOrder.ToLower() == "desc";

                query = sortBy switch
                {
                    "InvoiceNo" => isDescending ? query.OrderByDescending(i => i.InvoiceNo) : query.OrderBy(i => i.InvoiceNo),
                    "IssueDate" => isDescending ? query.OrderByDescending(i => i.IssueDate) : query.OrderBy(i => i.IssueDate),
                    "SubmissionDate" => isDescending ? query.OrderByDescending(i => i.IssueDate) : query.OrderBy(i => i.IssueDate),
                    "SupplierName" => isDescending ? query.OrderByDescending(i => i.Supplier.CompanyName) : query.OrderBy(i => i.Supplier.CompanyName),
                    "CustomerName" => isDescending
                        ? query.OrderByDescending(i =>
                            i.Customer != null
                                ? i.Customer.CompanyName
                                : i.PublicCustomer!.CompanyName)
                        : query.OrderBy(i =>
                            i.Customer != null
                                ? i.Customer.CompanyName
                                : i.PublicCustomer!.CompanyName),

                    "BuyerName" => isDescending
                        ? query.OrderByDescending(i =>
                            i.Customer != null
                                ? i.Customer.CompanyName
                                : i.PublicCustomer!.CompanyName)
                        : query.OrderBy(i =>
                            i.Customer != null
                                ? i.Customer.CompanyName
                                : i.PublicCustomer!.CompanyName),

                    "TotalAmount" => isDescending ? query.OrderByDescending(i => i.TotalAmountIncTax) : query.OrderBy(i => i.TotalAmountIncTax),
                    "UUID" => isDescending ? query.OrderByDescending(i => i.UUID) : query.OrderBy(i => i.UUID),
                    "SubmissionId" => isDescending ? query.OrderByDescending(i => i.SubmissionID) : query.OrderBy(i => i.SubmissionID),
                    "DocumentType" => isDescending ? query.OrderByDescending(i => i.DocTypeCode) : query.OrderBy(i => i.DocTypeCode),
                    "LHDNStatus" => isDescending ? query.OrderByDescending(i => i.LHDNStatus) : query.OrderBy(i => i.LHDNStatus),
                    "InternalStatus" => isDescending ? query.OrderByDescending(i => i.InternalStatus) : query.OrderBy(i => i.InternalStatus),
                    "RejectedDate" => isDescending ? query.OrderByDescending(i => i.RejectedTimestamp) : query.OrderBy(i => i.RejectedTimestamp),
                    "UpdatedDate" => isDescending ? query.OrderByDescending(i => i.LastUpdated) : query.OrderBy(i => i.LastUpdated),
                    _ => query.OrderBy(i => i.InvoiceNo) // Default sorting
                };
            }

            return query;
        }

        public async Task<IActionResult> OnPostDeleteFromListAsync([FromBody] DeleteInvoiceInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.InvoiceNo))
            {
                return new JsonResult(new { success = false, message = "Invalid invoice number." });
            }

            // IDOR guard: a user may only delete their own company's invoice (no LHDN backstop on local delete).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, input.InvoiceNo))
            {
                _logger.LogWarning("DeleteFromList denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, input.InvoiceNo);
                return new JsonResult(new { success = false, message = "You are not authorized to delete this invoice." });
            }

            try
            {
                var invoice = await _context.InvoiceHeaders
                    .FirstOrDefaultAsync(i => i.InvoiceNo == input.InvoiceNo && i.InternalStatusId == "Draft");

                if (invoice == null)
                {
                    return new JsonResult(new { success = false, message = "Invoice not found or already deleted." });
                }

                // Log to InvoiceHistory
                _context.InvoiceHistories.Add(new InvoiceHistory
                {
                    InvoiceNo = input.InvoiceNo,
                    Action = "Deleted",
                    Timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")),
                    PerformedBy = User.Identity?.Name ?? "System",
                    Remarks = "Invoice deleted from invoice list"
                });

                // Remove invoice record
                _context.InvoiceHeaders.Remove(invoice);

                // Save changes to the database
                await _context.SaveChangesAsync();

                // Delete the corresponding JSON draft file from the physical folder
                var jsonPath = _jsonFileService.GetExistingFilePath(input.InvoiceNo);
                if (!string.IsNullOrEmpty(jsonPath) && System.IO.File.Exists(jsonPath))
                {
                    try
                    {
                        System.IO.File.Delete(jsonPath);
                        _logger.LogInformation("Deleted JSON file for invoice {InvoiceNo} at {Path}", input.InvoiceNo, jsonPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete JSON file for invoice {InvoiceNo} at {Path}", input.InvoiceNo, jsonPath);
                    }
                }

                return new JsonResult(new { success = true, message = $"Invoice {input.InvoiceNo} deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice {InvoiceNo}", input.InvoiceNo);
                return new JsonResult(new { success = false, message = "Error deleting invoice." });
            }
        }

        // DTO input for Delete
        public class DeleteInvoiceInput
        {
            public string InvoiceNo { get; set; } = null!;
        }


        public async Task<IActionResult> OnPutRejectDocumentAsync(string documentId, string rejectionReason, string tin)
        {
            _logger.LogInformation("🚀 RejectDocument handler called with documentId: {documentId}, reason: {rejectionReason}, frontend tin: {tin}", documentId, rejectionReason, tin);

            // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceByUuidAsync(User, _context, documentId))
            {
                _logger.LogWarning("RejectDocument denied: user {User} cannot access document {DocumentId}.", User.Identity?.Name, documentId);
                return new JsonResult(new { success = false, message = "You are not authorized to reject this document." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(rejectionReason))
                {
                    _logger.LogWarning("❌ BadRequest: Document ID or rejection reason is missing.");
                    return BadRequest("Document ID and rejection reason are required.");
                }

                // Get the current user's TIN for the API call (ignore frontend TIN)
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("❌ No logged-in user found.");
                    return StatusCode(401, "User not logged in.");
                }

                var userTin = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userTin))
                {
                    _logger.LogError("❌ No TIN found for user {userId}.", user.Id);
                    return StatusCode(500, "User's TIN is missing.");
                }

                _logger.LogInformation("🔑 Using logged-in user's TIN for LHDN API: {userTin} (instead of frontend TIN: {tin})", userTin, tin);

                // Check rejection constraints
                _logger.LogInformation("🔍 Checking rejection constraints for document {documentId}...", documentId);
                var rejectionConstraintsCheck = await CheckRejectionConstraints(documentId);
                if (rejectionConstraintsCheck != null)
                {
                    _logger.LogWarning("⚠️ Rejection constraints check failed for document {documentId}", documentId);
                    return rejectionConstraintsCheck;
                }

                // FIRST: Call LHDN API to reject the document using user's TIN
                _logger.LogInformation("📡 Calling LHDN RejectDocument API for document {documentId} with user TIN {userTin}...", documentId, userTin);
                string apiResponse;
                try
                {
                    apiResponse = await _lhdnApiService.RejectDocumentAsync(documentId, rejectionReason, userTin);
                    _logger.LogInformation(" LHDN API Response: {response}", apiResponse);
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "❌ LHDN API call failed for document {documentId} with TIN {userTin}", documentId, userTin);
                    return StatusCode(500, $"Failed to reject document in LHDN API: {apiEx.Message}");
                }

                // SECOND: Update local database only after API success
                _logger.LogInformation("💾 LHDN API successful, now updating local database for document {documentId}...", documentId);
                var dbResult = await UpdateLocalDatabaseForRejection(documentId, rejectionReason);
                if (dbResult != null)
                {
                    _logger.LogError("❌ Database update failed for document {documentId}", documentId);
                    return dbResult;
                }

                _logger.LogInformation(" Document {documentId} successfully rejected in both LHDN API and local database", documentId);
                return new JsonResult(new { message = "Document rejection successfully processed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error rejecting document with ID: {DocumentId}", documentId);
                return StatusCode(500, "An error occurred while rejecting the document.");
            }
        }

        private async Task<IActionResult?> CheckRejectionConstraints(string documentId)
        {
            _logger.LogInformation("Checking rejection constraints for document {documentId}...", documentId);

            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer).Include(i => i.PublicCustomer)
                .FirstOrDefaultAsync(i => i.UUID == documentId);

            if (invoice == null)
            {
                _logger.LogWarning($"Invoice with ID {documentId} not found.");
                return NotFound($"Invoice with ID {documentId} not found.");
            }

            // Ensure LHDNStatus is Valid and InternalStatus is not RequestReject
            if (invoice.LHDNStatusId != "Valid" || invoice.InternalStatusId == "RequestReject")
            {
                _logger.LogWarning($"Invoice {documentId} cannot be rejected. LHDNStatusId: {invoice.LHDNStatusId}, InternalStatusId: {invoice.InternalStatusId}");
                return BadRequest("Invoice cannot be rejected. LHDNStatus must be 'Valid' and the invoice must not be in 'RequestReject' status.");
            }

            // Validate 72-hour window
            var timeCheck = await ValidateRejectionOrCancellationWindow(documentId);
            if (timeCheck != null) return timeCheck;

            return null; // No issues found
        }

        public async Task<IActionResult> OnPutCancelDocumentAsync(string documentId, string cancellationReason, string tin)
        {
            _logger.LogInformation("CancelDocument handler called with documentId: {DocumentId}, reason: {CancellationReason}, tin: {Tin}", documentId, cancellationReason, tin);

            // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceByUuidAsync(User, _context, documentId))
            {
                _logger.LogWarning("CancelDocument denied: user {User} cannot access document {DocumentId}.", User.Identity?.Name, documentId);
                return new JsonResult(new { success = false, message = "You are not authorized to cancel this document." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(cancellationReason))
                {
                    _logger.LogWarning("BadRequest: Document ID or cancellation reason is missing.");
                    return BadRequest("Document ID and cancellation reason are required.");
                }

                // Check cancellation constraints
                var cancellationConstraintsCheck = await CheckCancellationConstraints(documentId);
                if (cancellationConstraintsCheck != null) return cancellationConstraintsCheck;

                // Proceed to cancel document in LHDN API first
                _logger.LogInformation($"📡 Calling LHDN CancelDocument API for document {documentId}...");
                string response = await _lhdnApiService.CancelDocumentAsync(documentId, cancellationReason, tin);
                _logger.LogInformation($" LHDN API cancellation successful for document {documentId}");

                // Update local database (don't fail if this has issues since LHDN API already succeeded)
                try
                {
                    _logger.LogInformation($"💾 Updating local database for cancelled document {documentId}...");
                    var dbErrorResult = await CancelDocumentAndSaveAsync(documentId, cancellationReason);

                    if (dbErrorResult != null)
                    {
                        _logger.LogError($"❌ Actual database update error for {documentId}.");
                        return dbErrorResult;
                    }
                    else
                    {
                        _logger.LogInformation($" Database update successful for cancelled document {documentId}");
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, $"⚠️ Database update failed for cancelled document {documentId}, but LHDN API succeeded. Showing success to user.");
                }

                return new JsonResult(new { success = true, message = "Document cancellation successfully processed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling document with ID: {DocumentId}", documentId);
                return StatusCode(500, "An error occurred while canceling the document.");
            }
        }

        private async Task<IActionResult?> CheckCancellationConstraints(string documentId)
        {
            _logger.LogInformation("Checking cancellation constraints for document {documentId}...", documentId);

            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer).Include(i => i.PublicCustomer)
                .FirstOrDefaultAsync(i => i.UUID == documentId);

            if (invoice == null)
            {
                _logger.LogWarning($"Invoice with ID {documentId} not found.");
                return NotFound($"Invoice with ID {documentId} not found.");
            }

            // Ensure LHDNStatus is Valid and InternalStatus is RequestReject
            if (invoice.LHDNStatusId != "Valid")// || invoice.InternalStatusId != "RequestReject")
            {
                _logger.LogWarning($"Invoice {documentId} cannot be canceled. LHDNStatusId: {invoice.LHDNStatusId}, InternalStatusId: {invoice.InternalStatusId}");
                return BadRequest("Invoice cannot be canceled. LHDNStatus must be 'Valid'.");
            }

            // Validate 72-hour window
            var timeCheck = await ValidateRejectionOrCancellationWindow(documentId);
            if (timeCheck != null) return timeCheck;

            return null; // No issues found
        }

        private async Task<IActionResult?> ValidateRejectionOrCancellationWindow(string documentId)
        {
            _logger.LogInformation("Validating 72-hour window for document {documentId}...", documentId);

            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer).Include(i => i.PublicCustomer)
                .FirstOrDefaultAsync(i => i.UUID == documentId);

            if (invoice == null || !invoice.DateTimeValidated.HasValue)
            {
                _logger.LogWarning($"Invoice found but Validated Date is missing for document {documentId}.", documentId);
                return NotFound($"Invoice with ID {documentId} found, but Validated Date is missing.");
            }

            DateTime dateTimeValidated = invoice.DateTimeValidated.Value;
            DateTime deadline = dateTimeValidated.AddHours(72);
            _logger.LogInformation($"DateTimeValidated: {dateTimeValidated}, Deadline: {deadline}, Current Time: {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"))}");

            if (TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")) > deadline)
            {
                string errorMessage = "The 72-hour window for rejection or cancellation has expired.";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }

            return null; // No issues found
        }
        private async Task<IActionResult?> UpdateLocalDatabaseForRejection(string documentId, string rejectionReason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("No logged-in user found.");
                return StatusCode(401, "User not logged in.");
            }

            var invoice = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer).Include(i => i.PublicCustomer)
                .FirstOrDefaultAsync(i => i.UUID == documentId);

            if (invoice == null)
            {
                _logger.LogWarning($"Invoice with ID {documentId} not found.");
                return NotFound($"Invoice with ID {documentId} not found.");
            }

            string rejectedByUser = User.Identity?.Name ?? "Unknown";
            var rejectedTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));

            // Update invoice
            invoice.InternalStatusId = "RequestReject";
            invoice.RejectedBy = rejectedByUser;
            invoice.RejectedReason = rejectionReason;
            invoice.RejectedTimestamp = rejectedTime;
            invoice.LastUpdated = rejectedTime;
            _context.InvoiceHeaders.Update(invoice);

            // Email notifications
            if (_configuration.GetValue<bool>("EmailConfiguration:Notifications:EnableRejectionEmails"))
            {
                try
                {
                    if (_eInvoiceEmailService == null)
                    {
                        _logger.LogError("_eInvoiceEmailService is null.");
                        return StatusCode(500, "Email service unavailable.");
                    }

                    var buyerEmail = invoice.Customer != null
                        ? invoice.Customer.Email
                        : invoice.PublicCustomer?.Email;

                    if (string.IsNullOrEmpty(buyerEmail) || string.IsNullOrEmpty(invoice.Supplier?.Email))
                    {
                        return BadRequest("Customer or supplier email missing.");
                    }

                    var customerParty = invoice.Customer ?? new PartyInfo
                    {
                        CompanyName = invoice.PublicCustomer?.CompanyName ?? "",
                        Email = invoice.PublicCustomer?.Email ?? "",
                        TIN = invoice.PublicCustomer?.TIN ?? ""
                    };

                    _logger.LogInformation($"📄 Generating fresh PDF for rejected invoice {invoice.InvoiceNo} before emailing...");
                    await _pdfGeneratorService.GeneratePdfAsync(invoice.InvoiceNo);

                    // Send the email with the newly generated PDF
                    _eInvoiceEmailService.SendRejectionNotificationEmail(
                        customerParty,
                        invoice.Supplier,
                        invoice.InvoiceNo,
                        rejectionReason,
                        rejectedTime
                    );

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending rejection email.");
                    return StatusCode(500, "Failed to send rejection email.");
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Local database updated successfully for document {documentId}");
            return null; // Success
        }



        /// <summary>
        /// Processes document cancellation and updates the database.
        /// </summary>
        private async Task<IActionResult?> CancelDocumentAndSaveAsync(string documentId, string cancellationReason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("No logged-in user found.");
                return StatusCode(401, "User not logged in.");
            }

            var userTin = await _context.UserCompanies
                .Where(uc => uc.UserId == user.Id)
                .Include(uc => uc.PartyInfo)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userTin))
            {
                _logger.LogError("No TIN found for user {userId}.", user.Id);
                return StatusCode(500, "User's TIN is missing.");
            }

            _logger.LogInformation($"User {user.Email} is assigned to company TIN: {userTin}");

            // Try to fetch DocumentSummary, but don't fail if rate limited (since LHDN API already succeeded)
            DocumentSummary? documentSummary = null;
            try
            {
                string accessToken = await _tokenService.GetAccessTokenForTIN(userTin);
                _logger.LogInformation($"Fetching document summary for {documentId}...");
                documentSummary = await _lhdnApiService.GetDocumentDetailsAsync(documentId, accessToken);
                _logger.LogInformation($" Document summary retrieved successfully for {documentId}");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                _logger.LogWarning($"⚠️ LHDN API rate limited when fetching document details for {documentId}. Proceeding with database update since LHDN cancellation already succeeded.");
                // Continue without document summary - LHDN API already succeeded
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"⚠️ Could not fetch document summary for {documentId}. Proceeding with database update since LHDN cancellation already succeeded.");
                // Continue without document summary - LHDN API already succeeded
            }

            var invoice = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer).Include(i => i.PublicCustomer)
                    .FirstOrDefaultAsync(i => i.UUID == documentId);

            if (invoice == null)
            {
                _logger.LogWarning($"Invoice with ID {documentId} not found.");
                return NotFound($"Invoice with ID {documentId} not found.");
            }

            invoice.InternalStatusId = "Cancelled";
            invoice.LHDNStatusId = "Cancelled";
            invoice.CancelDateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
            invoice.LastUpdated = invoice.CancelDateTime;

            _context.InvoiceHeaders.Update(invoice);

            if (documentSummary != null)
            {
                documentSummary.cancelDateTime = DateTime.UtcNow;
                documentSummary.internalStatus = "Cancelled";
                documentSummary.lhdnStatus = "Cancelled";
                documentSummary.status = "Cancelled";
                _logger.LogInformation($" DocumentSummary updated for document ID {documentId}");
            }
            else
            {
                _logger.LogInformation($"⚠️ DocumentSummary not available for {documentId} - database update will proceed without it");
            }

            try
            {
                _logger.LogInformation($"💾 Saving database changes for cancelled document {documentId}...");
                await _context.SaveChangesAsync();
                _logger.LogInformation($" Database successfully updated for cancelled document {documentId}");
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, $"❌ Critical error: Failed to save database changes for cancelled document {documentId}");
                return StatusCode(500, $"Critical database error for document {documentId}");
            }

            if (_configuration.GetValue<bool>("EmailConfiguration:Notifications:EnableCancellationEmails"))
            {
                try
                {
                    if (_eInvoiceEmailService == null)
                    {
                        _logger.LogWarning("Email service is null. Skipping cancellation email notification.");
                    }
                    else
                    {
                        // 🔥 HYBRID FIX: Retrieve the correct email
                        var buyerEmail = invoice.Customer != null ? invoice.Customer.Email : invoice.PublicCustomer?.Email;

                        if (string.IsNullOrEmpty(buyerEmail))
                        {
                            _logger.LogWarning($"Customer email is missing for invoice {documentId}. Skipping email notification.");
                        }
                        else if (invoice.Supplier == null || string.IsNullOrEmpty(invoice.Supplier.Email))
                        {
                            _logger.LogWarning($"Supplier email is missing for invoice {documentId}. Skipping email notification.");
                        }
                        else
                        {
                            var customerParty = invoice.Customer ?? new PartyInfo
                            {
                                CompanyName = invoice.PublicCustomer?.CompanyName ?? "",
                                Email = invoice.PublicCustomer?.Email ?? "",
                                TIN = invoice.PublicCustomer?.TIN ?? ""
                            };

                            _logger.LogInformation($"📄 Generating fresh PDF for cancelled invoice {invoice.InvoiceNo} before emailing...");
                            await _pdfGeneratorService.GeneratePdfAsync(invoice.InvoiceNo);

                            // Send the email with the newly generated PDF
                            _eInvoiceEmailService.SendCancellationNotificationEmail(
                                customerParty,
                                invoice.Supplier,
                                invoice.InvoiceNo,
                                cancellationReason,
                                invoice.LastUpdated ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"))
                            );
                            _logger.LogInformation($" Cancellation email sent successfully for document {documentId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"⚠️ Error sending cancellation email for document {documentId}. Database update succeeded, but email failed.");
                }
            }

            return null;
        }


        public async Task<IActionResult> OnGetExportAsync(string fileType, string invoiceDirection, string documentType = "", DateTime? submissionDateFrom = null, DateTime? submissionDateTo = null, string internalStatusId = "")
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }
                UserType = user.UserType;

                // ENFORCE: Prevent Buyers from exporting Sent/Draft invoices via URL manipulation
                if (UserType == "Buyer")
                {
                    invoiceDirection = "Received";
                }

                var userTINs = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .Distinct()
                    .ToListAsync();

                if (!userTINs.Any())
                {
                    _logger.LogWarning($"No TINs found for user: {user.Id}. Export aborted.");
                    return RedirectToPage("/Invoices/InvoiceLists");
                }

                var query = _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer).Include(i => i.PublicCustomer)
                    .Include(i => i.InvoiceLines).ThenInclude(l => l.InvoiceTaxes)
                    .AsQueryable();

                // Apply Invoice Direction Filtering
                if (!string.IsNullOrEmpty(invoiceDirection) && invoiceDirection != "All")
                {
                    var selfBilledTypes = new[] { "11", "12", "13", "14" };

                    // ✅ ADDED SUPPORT FOR DRAFT EXPORTS
                    if (invoiceDirection == "Draft")
                    {
                        query = query.Where(i =>
                            i.InternalStatusId == "Draft" &&
                            (
                                (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN)) ||
                                (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN))
                            )
                        );
                    }
                    else if (invoiceDirection == "Sent")
                    {
                        // Note: Standard 'Sent' excludes drafts.
                        // If you want Sent to INCLUDE drafts, remove: i.InternalStatusId != "Draft" &&
                        query = query.Where(i =>
                            i.InternalStatusId != "Draft" &&
                            (
                                (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN)) ||
                                (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN))
                            )
                        );
                    }
                    else if (invoiceDirection == "Received")
                    {
                        query = query.Where(i =>
                            i.InternalStatusId != "Draft" &&
                            i.InternalStatusId != "Invalid" &&
                            i.LHDNStatusId != "Invalid" &&
                            !string.IsNullOrEmpty(i.UUID) &&
                            (
                                (!selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Customer != null ? i.Customer.TIN : i.PublicCustomer!.TIN)) ||
                                (selfBilledTypes.Contains(i.DocTypeCode) && userTINs.Contains(i.Supplier.TIN))
                            )
                        );
                    }
                }

                // ✅ FIXED: Date range bug. Make the EndDate inclusive to 23:59:59
                if (submissionDateFrom.HasValue && submissionDateTo.HasValue)
                {
                    var endOfDay = submissionDateTo.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.IssueDate >= submissionDateFrom.Value.Date && i.IssueDate <= endOfDay);
                }

                // Apply Internal Status Filtering
                if (!string.IsNullOrEmpty(internalStatusId))
                {
                    query = query.Where(i => i.InternalStatusId == internalStatusId);
                }

                // Apply Document Type Filtering
                if (!string.IsNullOrEmpty(documentType))
                {
                    query = query.Where(i => i.DocTypeCode == documentType);
                }

                // Fetch the filtered invoices
                var invoices = await query.ToListAsync();
                string timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")).ToString("ddMMyyyy_HHmmss");
                string fileName = $"Invoice_{user.FullName}_{timestamp}.{fileType}";

                if (fileType == "csv")
                {
                    var csvData = GenerateCsv(invoices);
                    return File(Encoding.UTF8.GetBytes(csvData), "text/csv", fileName);
                }
                else if (fileType == "xlsx")
                {
                    var xlsxData = GenerateExcel(invoices);
                    return File(xlsxData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }

                return BadRequest("Invalid export format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting invoices");
                return BadRequest("Error exporting invoices.");
            }
        }

        private string GenerateCsv(List<InvoiceHeader> invoices)
        {
            var builder = new StringBuilder();

            builder.AppendLine("InvoiceNo,IssueDate,PoDoNo,DocTypeCode,Currency,ExchangeRate,BillingFrequency,StartDate,EndDate,BankAccountNumber,BankName,AttentionTo,PaymentTerms,CounterpartyTIN,ItemDescription,Quantity,UnitPrice,DiscountAmount,UnitOfMeasure,ClassificationCode,TaxCategory,TaxPercentage,UUID,SubmissionID,Supplier,Customer,Status");

            foreach (var invoice in invoices)
            {
                var supplierName = EscapeCsv(invoice.Supplier?.CompanyName);
                var customerName = EscapeCsv(invoice.Customer != null ? invoice.Customer.CompanyName : invoice.PublicCustomer?.CompanyName);
                var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(invoice.DocTypeCode);
                var counterpartyTIN = EscapeCsv(isSelfBilled ? invoice.Supplier?.TIN : (invoice.Customer?.TIN ?? invoice.PublicCustomer?.TIN));

                // ✅ Safely handle nullable DateTimes to ensure formatting never crashes
                string issueDateStr = invoice.IssueDate?.ToString("yyyy-MM-dd") ?? "";
                string startDateStr = invoice.StartDate?.ToString("yyyy-MM-dd") ?? "";
                string endDateStr = invoice.EndDate?.ToString("yyyy-MM-dd") ?? "";

                string commonData = $"{EscapeCsv(invoice.InvoiceNo)},{issueDateStr},{EscapeCsv(invoice.PoDoNo)},{EscapeCsv(invoice.DocTypeCode)},{EscapeCsv(invoice.Currency)},{invoice.ExchangeRate},{EscapeCsv(invoice.InvoicePeriod.ToString())},{startDateStr},{endDateStr},{EscapeCsv(invoice.BankAccountNo)},{EscapeCsv(invoice.BankName)},{EscapeCsv(invoice.Attention)},{EscapeCsv(invoice.PaymentTerms)},{counterpartyTIN}";
                string extraData = $"{EscapeCsv(invoice.UUID)},{EscapeCsv(invoice.SubmissionID)},{supplierName},{customerName},{EscapeCsv(invoice.InternalStatusId)}";

                if (invoice.InvoiceLines != null && invoice.InvoiceLines.Any())
                {
                    foreach (var line in invoice.InvoiceLines)
                    {
                        var tax = line.InvoiceTaxes?.FirstOrDefault();
                        string lineData = $"{EscapeCsv(line.ItemDescription)},{line.Quantity},{line.UnitPrice},{line.DiscountAmount},{EscapeCsv(line.UnitOfMeasure)},{EscapeCsv(line.ClassificationCode)},{EscapeCsv(tax?.TaxCategory)},{tax?.TaxPercentage}";
                        builder.AppendLine($"{commonData},{lineData},{extraData}");
                    }
                }
                else
                {
                    builder.AppendLine($"{commonData},,,,,,,,{extraData}");
                }
            }

            return builder.ToString();
        }

        // Assigns a cell value, mapping CLR types to ClosedXML's XLCellValue. Nulls become blank cells.
        private static void SetCell(IXLWorksheet ws, int row, int col, object? value)
        {
            var cell = ws.Cell(row, col);
            switch (value)
            {
                case null: cell.Value = Blank.Value; break;
                case string s: cell.Value = s; break;
                case decimal d: cell.Value = d; break;
                case double db: cell.Value = db; break;
                case int i: cell.Value = i; break;
                case long l: cell.Value = l; break;
                case bool b: cell.Value = b; break;
                case DateTime dt: cell.Value = dt; break;
                default: cell.Value = value.ToString(); break;
            }
        }

        private byte[] GenerateExcel(List<InvoiceHeader> invoices)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Invoices");

                string[] headers = {
            "InvoiceNo", "IssueDate", "PoDoNo", "DocTypeCode", "Currency", "ExchangeRate", "BillingFrequency", "StartDate", "EndDate", "BankAccountNumber",
            "BankName", "AttentionTo", "PaymentTerms", "CounterpartyTIN", "ItemDescription", "Quantity", "UnitPrice",
            "DiscountAmount", "UnitOfMeasure", "ClassificationCode", "TaxCategory", "TaxPercentage",
            "UUID", "SubmissionID", "Supplier", "Customer", "Status"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    SetCell(worksheet, 1, i + 1, headers[i]);
                }

                int row = 2;
                foreach (var invoice in invoices)
                {
                    var supplierName = invoice.Supplier?.CompanyName;
                    var customerName = invoice.Customer != null ? invoice.Customer.CompanyName : invoice.PublicCustomer?.CompanyName;
                    var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(invoice.DocTypeCode);
                    var counterpartyTIN = isSelfBilled ? invoice.Supplier?.TIN : (invoice.Customer?.TIN ?? invoice.PublicCustomer?.TIN);

                    if (invoice.InvoiceLines != null && invoice.InvoiceLines.Any())
                    {
                        foreach (var line in invoice.InvoiceLines)
                        {
                            var tax = line.InvoiceTaxes?.FirstOrDefault();

                            SetCell(worksheet, row, 1, invoice.InvoiceNo);
                            // Safely handle nullable DateTimes to ensure formatting never crashes
                            SetCell(worksheet, row, 2, invoice.IssueDate?.ToString("yyyy-MM-dd") ?? "");
                            SetCell(worksheet, row, 3, invoice.PoDoNo);
                            SetCell(worksheet, row, 4, invoice.DocTypeCode);
                            SetCell(worksheet, row, 5, invoice.Currency);
                            SetCell(worksheet, row, 6, invoice.ExchangeRate);
                            SetCell(worksheet, row, 7, invoice.InvoicePeriod.ToString());
                            SetCell(worksheet, row, 8, invoice.StartDate?.ToString("yyyy-MM-dd") ?? "");
                            SetCell(worksheet, row, 9, invoice.EndDate?.ToString("yyyy-MM-dd") ?? "");
                            SetCell(worksheet, row, 10, invoice.BankAccountNo);
                            SetCell(worksheet, row, 11, invoice.BankName);
                            SetCell(worksheet, row, 12, invoice.Attention);
                            SetCell(worksheet, row, 13, invoice.PaymentTerms);
                            SetCell(worksheet, row, 14, counterpartyTIN);

                            SetCell(worksheet, row, 15, line.ItemDescription);
                            SetCell(worksheet, row, 16, line.Quantity);
                            SetCell(worksheet, row, 17, line.UnitPrice);
                            SetCell(worksheet, row, 18, line.DiscountAmount);
                            SetCell(worksheet, row, 19, line.UnitOfMeasure);
                            SetCell(worksheet, row, 20, line.ClassificationCode);
                            SetCell(worksheet, row, 21, tax?.TaxCategory);
                            SetCell(worksheet, row, 22, tax?.TaxPercentage);

                            SetCell(worksheet, row, 23, invoice.UUID);
                            SetCell(worksheet, row, 24, invoice.SubmissionID);
                            SetCell(worksheet, row, 25, supplierName);
                            SetCell(worksheet, row, 26, customerName);
                            SetCell(worksheet, row, 27, invoice.InternalStatusId);

                            row++;
                        }
                    }
                    else
                    {
                        SetCell(worksheet, row, 1, invoice.InvoiceNo);
                        SetCell(worksheet, row, 2, invoice.IssueDate?.ToString("yyyy-MM-dd") ?? "");
                        SetCell(worksheet, row, 3, invoice.PoDoNo);
                        SetCell(worksheet, row, 4, invoice.DocTypeCode);
                        SetCell(worksheet, row, 5, invoice.Currency);
                        SetCell(worksheet, row, 6, invoice.ExchangeRate);
                        SetCell(worksheet, row, 7, invoice.InvoicePeriod.ToString());
                        SetCell(worksheet, row, 8, invoice.StartDate?.ToString("yyyy-MM-dd") ?? "");
                        SetCell(worksheet, row, 9, invoice.EndDate?.ToString("yyyy-MM-dd") ?? "");
                        SetCell(worksheet, row, 10, invoice.BankAccountNo);
                        SetCell(worksheet, row, 11, invoice.BankName);
                        SetCell(worksheet, row, 12, invoice.Attention);
                        SetCell(worksheet, row, 13, invoice.PaymentTerms);
                        SetCell(worksheet, row, 14, counterpartyTIN);

                        SetCell(worksheet, row, 23, invoice.UUID);
                        SetCell(worksheet, row, 24, invoice.SubmissionID);
                        SetCell(worksheet, row, 25, supplierName);
                        SetCell(worksheet, row, 26, customerName);
                        SetCell(worksheet, row, 27, invoice.InternalStatusId);

                        row++;
                    }
                }

                worksheet.Columns().AdjustToContents();

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\""; // Escapes quotes and wraps the string
            }
            return value;
        }

        // Add this handler to InvoiceLists.cshtml.cs
        public async Task<IActionResult> OnPostSubmitFromListAsync(string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                TempData["ErrorMessage"] = "Invoice number is required.";
                return RedirectToPage();
            }

            // IDOR guard (defense-in-depth alongside LHDN's per-TIN token scoping).
            if (!await EINVWORLD.Helpers.UserExtensions.CanAccessInvoiceAsync(User, _context, invoiceNo))
            {
                _logger.LogWarning("SubmitFromList denied: user {User} cannot access invoice {InvoiceNo}.", User.Identity?.Name, invoiceNo);
                TempData["ErrorMessage"] = "You are not authorized to submit this invoice.";
                return RedirectToPage();
            }

            try
            {
                var invoice = await _context.InvoiceHeaders
                    .Include(i => i.Supplier)
                    .Include(i => i.Customer).Include(i => i.PublicCustomer)
                    .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = $"Invoice {invoiceNo} not found.";
                    return RedirectToPage();
                }

                if (invoice.InternalStatusId != "Draft")
                {
                    TempData["ErrorMessage"] = $"Invoice {invoiceNo} is not in Draft status.";
                    return RedirectToPage();
                }

                // Resolve the issuer TIN so submission uses the per-TIN token + onbehalfof header and an
                // ownership check (consistent with Create Invoice); falls back to the session token below
                // if it can't be resolved.
                var submitterTin = EINVWORLD.Helpers.TinHelper.ResolveSubmitterTin(invoice);
                if (!string.IsNullOrWhiteSpace(submitterTin)
                    && !await EINVWORLD.Helpers.UserExtensions.OwnsTinAsync(User, _context, submitterTin))
                {
                    _logger.LogWarning("🚫 User not authorized to submit {InvoiceNo} under issuer TIN {TIN}.", invoiceNo, submitterTin);
                    TempData["ErrorMessage"] = "You are not authorized to submit this invoice.";
                    return RedirectToPage();
                }

                var jsonPath = _jsonFileService.GetExistingFilePath(invoiceNo);
                if (string.IsNullOrEmpty(jsonPath) || !System.IO.File.Exists(jsonPath))
                {
                    TempData["ErrorMessage"] = $"Draft JSON for invoice {invoiceNo} does not exist.";
                    return RedirectToPage();
                }

                var invoiceJson = await System.IO.File.ReadAllTextAsync(jsonPath);
                var encodedDocument = _lhdnApiService.Base64Encode(invoiceJson);
                var documentHash = _lhdnApiService.ComputeSHA256Hash(invoiceJson);

                var documents = new List<eInvWorld.Models.JsonModels.Documents>
                {
                    new eInvWorld.Models.JsonModels.Documents
                    {
                        Format = "JSON",
                        DocumentHash = documentHash,
                        CodeNumber = invoiceNo,
                        Document = encodedDocument
                    }
                };

                var accessToken = HttpContext.Session.GetString("AccessToken");
                if (string.IsNullOrEmpty(accessToken))
                {
                    TempData["ErrorMessage"] = "Access token missing or expired. Please log in again.";
                    return RedirectToPage();
                }

                // Atomic double-submit guard: only one concurrent request wins this claim; others are
                // blocked so the document can't be posted to LHDN twice (the Draft-status check is not atomic).
                if (!await EINVWORLD.Helpers.InvoiceSubmissionGuard.TryClaimAsync(_context, invoiceNo))
                {
                    _logger.LogWarning("[Guard] Concurrent submit blocked for {InvoiceNo}.", invoiceNo);
                    TempData["ErrorMessage"] = $"Invoice {invoiceNo} is already being submitted. Please wait a moment and refresh.";
                    return RedirectToPage();
                }

                var apiResponseJson = await _lhdnApiService.SubmitDocumentsAsync(documents, submitterTin);
                _logger.LogInformation($"[LHDN API Raw Response] {apiResponseJson}");

                var apiResponse = JsonConvert.DeserializeObject<SuccessSubmit>(apiResponseJson);
                var accepted = apiResponse?.acceptedDocuments?.FirstOrDefault();

                if (accepted == null || string.IsNullOrEmpty(accepted.uuid))
                {
                    TempData["ErrorMessage"] = $"Submission accepted but UUID missing for Invoice {invoiceNo}.";
                    return RedirectToPage();
                }

                invoice.UUID = accepted.uuid;
                invoice.SubmissionID = apiResponse?.submissionUID;
                invoice.LHDNStatusId = "Submitted";
                invoice.InternalStatusId = "Submitted";
                invoice.DateTimeReceived = GetMYTime();
                invoice.LastUpdated = GetMYTime();
                invoice.UpdatedBy = User.Identity?.Name ?? "System";

                _context.InvoiceHeaders.Update(invoice);

                _context.InvoiceHistories.Add(new InvoiceHistory
                {
                    InvoiceNo = invoiceNo,
                    Action = "Submitted",
                    Timestamp = GetMYTime(),
                    PerformedBy = User.Identity?.Name ?? "System",
                    Remarks = $"Submitted from Invoice List. UUID: {accepted.uuid}"
                });

                await _context.SaveChangesAsync();

                DocumentSummary? documentStatus = null;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        documentStatus = await _lhdnApiService.GetDocumentDetailsAsync(accepted.uuid, accessToken);
                        if (documentStatus != null) break;
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("422") || ex.Message.Contains("404"))
                    {
                        await Task.Delay(2000);
                    }
                }

                if (documentStatus != null)
                {
                    invoice.LHDNStatusId = documentStatus.status;
                    invoice.InternalStatusId = documentStatus.status;
                    invoice.LastUpdated = GetMYTime();

                    _context.InvoiceHeaders.Update(invoice);
                    _context.InvoiceHistories.Add(new InvoiceHistory
                    {
                        InvoiceNo = invoiceNo,
                        Action = "StatusSync",
                        Timestamp = GetMYTime(),
                        PerformedBy = User.Identity?.Name ?? "System",
                        Remarks = $"Fetched LHDN status: {documentStatus.status}"
                    });

                    await _context.SaveChangesAsync();
                }

                _jsonFileService.MoveToStatusFolder(invoice.InvoiceNo, invoice.LHDNStatusId);

                TempData["SuccessMessage"] = $"Invoice {invoice.InvoiceNo} submitted successfully. UUID: {accepted.uuid}";
                return RedirectToPage(new { invoiceDirection = "Sent" });
            }
            catch (Exception ex)
            {
                await EINVWORLD.Helpers.InvoiceSubmissionGuard.ReleaseAsync(_context, invoiceNo);
                _logger.LogError(ex, $"❌ Error submitting invoice {invoiceNo} from list.");

                var failedInvoice = await _context.InvoiceHeaders.FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);
                if (failedInvoice != null)
                {
                    failedInvoice.InternalStatusId = "TransmissionError";
                    failedInvoice.LastUpdated = GetMYTime();

                    _context.InvoiceHistories.Add(new InvoiceHistory
                    {
                        InvoiceNo = invoiceNo,
                        Action = "Transmission Failed",
                        Timestamp = GetMYTime(),
                        PerformedBy = User.Identity?.Name ?? "System",
                        Remarks = $"API Submission failed: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 200))}"
                    });

                    _context.InvoiceHeaders.Update(failedInvoice);
                    await _context.SaveChangesAsync();
                }
                TempData["ErrorMessage"] = $"Error submitting invoice {invoiceNo}: {ex.Message}";
                return RedirectToPage();
            }
        }


        private DateTime GetMYTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
        }

        // API method to save user column preferences
        public async Task<IActionResult> OnPostSaveColumnPreferencesAsync([FromBody] Dictionary<string, bool> columnSettings)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                // Create or update user preferences
                var preferences = new Dictionary<string, object>();

                // Load existing preferences if they exist
                if (!string.IsNullOrEmpty(user.UserPreferences))
                {
                    try
                    {
                        preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(user.UserPreferences)
                                    ?? new Dictionary<string, object>();
                    }
                    catch
                    {
                        preferences = new Dictionary<string, object>();
                    }
                }

                // Update column preferences
                preferences["invoiceListColumns"] = columnSettings;

                // Save back to user
                user.UserPreferences = JsonConvert.SerializeObject(preferences);
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Column preferences saved for user {user.Id}");
                    return new JsonResult(new { success = true, message = "Column preferences saved successfully" });
                }
                else
                {
                    _logger.LogError($"Failed to save column preferences for user {user.Id}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    return new JsonResult(new { success = false, message = "Failed to save preferences" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving column preferences");
                return new JsonResult(new { success = false, message = "An error occurred while saving preferences" });
            }
        }

        // API method to load user column preferences
        public async Task<IActionResult> OnGetLoadColumnPreferencesAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                // Default column settings (all visible except always-hidden ones)
                var defaultSettings = new Dictionary<string, bool>
                {
                    { "col-checkbox", true },
                    { "col-invoice-no", true },
                    { "col-uuid", true },
                    { "col-submission-id", true },
                    { "col-supplier", true },
                    { "col-buyer", true },
                    { "col-submitted-date", true },
                    { "col-document-type", true },
                    { "col-total-amount", true },
                    { "col-lhdn-status", true },
                    { "col-internal-status", true },
                    { "col-rejected-date", true },
                    { "col-last-updated", true },
                    { "col-action", true }
                };

                // Load user preferences
                if (!string.IsNullOrEmpty(user.UserPreferences))
                {
                    try
                    {
                        var preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(user.UserPreferences);
                        if (preferences != null && preferences.ContainsKey("invoiceListColumns"))
                        {
                            var columnPrefs = JsonConvert.DeserializeObject<Dictionary<string, bool>>(preferences["invoiceListColumns"].ToString() ?? "");
                            if (columnPrefs != null)
                            {
                                return new JsonResult(new { success = true, data = columnPrefs });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error parsing user preferences for user {user.Id}");
                    }
                }

                // Return default settings if no preferences found
                return new JsonResult(new { success = true, data = defaultSettings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading column preferences");
                return new JsonResult(new { success = false, message = "An error occurred while loading preferences" });
            }
        }

        public async Task<JsonResult> OnPostSyncActiveSessionAsync([FromBody] List<string> visibleInvoiceNos)
        {
            if (visibleInvoiceNos == null || !visibleInvoiceNos.Any())
                return new JsonResult(new { success = true, updatedCount = 0 });

            int updatedCount = 0;

            // Only get invoices that are "Valid" (meaning we are waiting to see if the buyer rejects/cancels them)
            var invoicesToSync = await _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer)
                .Where(i => visibleInvoiceNos.Contains(i.InvoiceNo) && i.LHDNStatusId == "Valid")
                .ToListAsync();

            foreach (var invoice in invoicesToSync)
            {
                // Pass "UISession" to tell the helper to use the fast 60-second cooldown
                bool wasUpdated = await _invoiceSyncHelper.SyncLhdnInvoiceStatusAsync(invoice, "UISession");

                if (wasUpdated)
                {
                    updatedCount++;
                }

                // Tiny speed bump to protect against 429s during session refresh
                await Task.Delay(250);
            }

            return new JsonResult(new { success = true, updatedCount = updatedCount });
        }


    }
}
