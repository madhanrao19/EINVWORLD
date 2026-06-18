using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Helpers;
using EINVWORLD.Helpers;


namespace eInvWorld.Pages.Invoices
{
    public class DashboardBuyerModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardBuyerModel> _logger;

        public int TotalInvoices { get; set; }
        public int SubmittedInvoices { get; set; }
        public int ValidInvoices { get; set; }
        public int InvalidInvoices { get; set; }
        public int RejectRequestInvoices { get; set; }
        public int CancelledInvoices { get; set; }

        public DashboardBuyerModel(ApplicationDbContext context, ILogger<DashboardBuyerModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void OnGet()
        {
            try
            {
                _logger.LogInformation("Fetching invoice data...");

                var invoiceData = _context.InvoiceHeaders
                    .AsNoTracking()
                    .GroupBy(i => i.LHDNStatusId ?? "Unknown") // Replace null with "Unknown"
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionary(g => g.Status, g => g.Count);

                var invoiceDataInternal = _context.InvoiceHeaders
                    .AsNoTracking()
                    .GroupBy(i => i.InternalStatusId ?? "Unknown")
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionary(g => g.Status, g => g.Count);

                _logger.LogInformation("invoiceData: {@invoiceData}", invoiceData);
                _logger.LogInformation("invoiceDataInternal: {@invoiceDataInternal}", invoiceDataInternal);


                // Assign values (using dictionary lookup for better performance)
                TotalInvoices = invoiceData.Values.Sum();
                SubmittedInvoices = invoiceData.GetValueOrDefault("Submitted", 0);
                ValidInvoices = invoiceData.GetValueOrDefault("Valid", 0);
                InvalidInvoices = invoiceData.GetValueOrDefault("Invalid", 0);
                RejectRequestInvoices = invoiceDataInternal.GetValueOrDefault("RejectRequest", 0);
                CancelledInvoices = invoiceData.GetValueOrDefault("Cancelled", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice data.");
            }
        }
        
        public JsonResult OnGetInvoiceData()
        {
            try
            {
                var userTIN = User.GetUserTIN(_context);

                var invoices = _context.InvoiceHeaders
                    .Include(i => i.Customer)
                    .Where(i => i.Customer.TIN == userTIN)
                    .AsNoTracking()
                    .ToList();

                var docTypeNames = DocumentTypeDisplay.GetDisplayNames();

                _logger.LogInformation("User.Identity.Name: {UserIdentity}", User.Identity?.Name ?? "NULL");

                // Log invoices count before grouping
                _logger.LogInformation("Total invoices: {InvoiceCount}", invoices.Count);

                // Group invoices by user email (instead of company name)
                var invoicesByUser = invoices
                    .GroupBy(i => i.CreatedBy ?? "Unknown User") // If Customer.Email is null, assign "Unknown User"
                    .Select(g => new { User = g.Key, Count = g.Count() })
                    .ToList();

                // Log grouped invoice data
                _logger.LogInformation("Invoices grouped by user: {InvoicesByUser}", invoicesByUser);



                return new JsonResult(new
                {
                    StatusCounts = invoices.GroupBy(i => i.InternalStatusId ?? "Unknown")
                        .Select(g => new { Status = g.Key, Count = g.Count() }),

                    AmountByMonth = invoices.Where(i => i.IssueDate.HasValue)
                        .GroupBy(i => i.IssueDate!.Value.ToString("yyyy-MM"))
                        .Select(g => new { Month = g.Key, Total = g.Sum(i => i.TotalAmountIncTax ?? 0) }),

                    InvoicesByCustomer = invoicesByUser,

                    //InvoicesByCustomer = invoices
                    //    .Where(i => i.Customer != null)
                    //    .GroupBy(i => i.Customer.CompanyName)
                    //    .Select(g => new { Customer = g.Key, Count = g.Count() }),

                    RejectedReasons = invoices
                        .Where(i => i.InternalStatusId == "RequestReject" || i.LHDNStatusId == "Cancelled")
                        .GroupBy(i => i.RejectedReason != null && CancelDocumentInput.GetReasons().Contains(i.RejectedReason) ? i.RejectedReason : "Others")
                        .Select(g => new { Reason = g.Key, Count = g.Count() })
                        .ToList(),

                    //RejectedVsApproved = new
                    //{
                    //    Rejected = invoices.Count(i => i.LHDNStatusId == "Invalid" || i.LHDNStatusId == "Cancelled"),
                    //    Approved = invoices.Count(i => i.LHDNStatusId == "Valid")
                    //},

                    InvoiceType = Enum.GetValues(typeof(EInvoiceDocumentType))
                        .Cast<EInvoiceDocumentType>()
                        .Select(type => new {
                            Type = docTypeNames.TryGetValue(type, out string? displayName) ? displayName : type.ToString(),
                            Count = invoices.Count(i => int.TryParse(i.DocTypeCode, out int code) && code == (int)type) // ✅ Fixed filtering
                        })
                        .ToList(),


                    MonthlyInvoiceTypes = invoices
                        .GroupBy(i => new { Month = i.IssueDate!.Value.ToString("yyyy-MM"), Type = i.DocTypeCode })
                        .Select(g => new
                        {
                            Months = g.Key.Month,
                            Type = docTypeNames.TryGetValue((EInvoiceDocumentType)int.Parse(g.Key.Type), out string? displayName) ? displayName : g.Key.Type,
                            Count = g.Count()
                        })
                        .ToList(),

                    DocTypeNames = docTypeNames?.ToDictionary(k => k.Key.ToString(), v => v.Value) ?? new Dictionary<string, string>()

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice chart data.");
                return new JsonResult(new { error = "Failed to fetch data." });
            }
        }
    }
}
