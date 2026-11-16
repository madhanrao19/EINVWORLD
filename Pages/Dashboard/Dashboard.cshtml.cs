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
using eInvWorld.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using EINVWORLD.Helpers;

namespace eInvWorld.Pages.Invoices
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardModel> _logger;

        public int TotalInvoices { get; set; }
        public int SubmittedInvoices { get; set; }
        public int ValidInvoices { get; set; }
        public int InvalidInvoices { get; set; }
        public int RejectRequestInvoices { get; set; }
        public int CancelledInvoices { get; set; }
        public int DraftInvoices { get; set; }
        public string UserType { get; set; } = null!;
        /// <summary>Signed-in user's full name for the welcome header (falls back to empty).</summary>
        public string DisplayName { get; set; } = string.Empty;
        public int TotalCompaniesCount { get; set; }
        public int TotalUsersCount { get; set; }
        public int TotalBuyerCount { get; set; }
        public int ActionInvalidCount { get; set; }
        public int ActionTransmissionErrorCount { get; set; }
        /// <summary>
        /// Distinct-invoice "Needs Attention" headline count (Phase 1D) — NOT a sum of
        /// ActionInvalidCount/ActionTransmissionErrorCount/RejectRequestInvoices/aging drafts, since
        /// an invoice can match more than one of those simultaneously. See InvoiceNeedsAttentionFilter.
        /// </summary>
        public int NeedsAttentionCount { get; set; }
        // Breakdown rows under the headline (Phase 1D) — all-time, same scope as NeedsAttentionCount,
        // deliberately separate from the legacy month-scoped ActionInvalidCount/ActionTransmissionErrorCount
        // tiles above so this new panel's numbers stay internally consistent without changing those tiles.
        public int NeedsAttentionInvalidCount { get; set; }
        public int NeedsAttentionTransmissionErrorCount { get; set; }
        public int NeedsAttentionRejectedCount { get; set; }
        public int NeedsAttentionAgingDraftsCount { get; set; }
        public decimal TotalValidAmount { get; set; }
        public decimal SubmittedAmount { get; set; }
        public decimal InvalidAmount { get; set; }
        public decimal CancelledAmount { get; set; }
        public decimal RejectRequestAmount { get; set; }
        public int? PrimaryCompanyId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterType { get; set; } = "Month"; // Default to Month

        [BindProperty(SupportsGet = true)]
        public DateTime? FilterDate { get; set; }
        public int BuyerTotalReceivedCount { get; set; }
        public decimal BuyerTotalReceivedAmount { get; set; }
        public int BuyerValidCount { get; set; }
        public decimal BuyerValidAmount { get; set; }
        public int BuyerRequestRejectCount { get; set; }
        public decimal BuyerRequestRejectAmount { get; set; }
        public int BuyerCancelledCount { get; set; }
        public decimal BuyerCancelledAmount { get; set; }
        public int BuyerTotalSuppliersCount { get; set; }
        private readonly eInvWorld.Services.FinanceTodayService _today;

        /// <summary>The Today card (Supplier dashboard); null when it couldn't be loaded.</summary>
        public eInvWorld.Services.FinanceTodayModel? Today { get; set; }

        public DashboardModel(ApplicationDbContext context, ILogger<DashboardModel> logger, eInvWorld.Services.FinanceTodayService today)
        {
            _context = context;
            _logger = logger;
            _today = today;
        }

        public List<eInvWorld.Models.InputModel.InvoiceHeader> RecentInvoices { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                Today = await _today.GetAsync(User, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Today card failed to load; the rest of the Dashboard still renders.");
            }

            try
            {
                _logger.LogInformation("📊 Fetching invoice stats for dashboard...");

                // Default filter date to today if not provided
                if (!FilterDate.HasValue) FilterDate = DateTime.Now;

                var user = User;
                if (user != null)
                {
                    var appUser = _context.Users.OrderBy(u => u.Id).FirstOrDefault(u => u.UserName == user.Identity!.Name);
                    UserType = appUser?.UserType ?? "Unknown";
                    DisplayName = appUser?.FullName ?? string.Empty;

                    var companyIds = user.GetUserCompanyIds(_context);
                    if (!companyIds.Any()) return;

                    PrimaryCompanyId = companyIds.OrderBy(c => c).FirstOrDefault();

                    // Base query: the company's own invoices, and what it receives once LHDN validated it (never the
                    // other company's drafts, errors or pending documents).
                    var tins = UserExtensions.TinsOf(_context, companyIds);
                    var invoiceQuery = _context.InvoiceHeaders
                        .AsNoTracking()
                        .Where(UserExtensions.VisibleToTins(tins));

                    ActionTransmissionErrorCount = invoiceQuery.Count(i => i.InternalStatusId == "TransmissionError");

                    // Needs Attention (Phase 1D): distinct-invoice count, not a sum of the four action
                    // tiles below — same predicate the Invoice List's "Needs Attention" chip filters on
                    // (InvoiceNeedsAttentionFilter), computed here before the date-range filter is applied
                    // so it reflects the whole company scope like the other action counters do.
                    // Breakdown rows go through the same Unresolved() rule so resolved invoices (marked
                    // resolved, or with a Valid Resend copy) drop out of the headline and the pills together.
                    var allInvoices = _context.InvoiceHeaders.AsNoTracking();
                    NeedsAttentionCount = InvoiceNeedsAttentionFilter.Apply(invoiceQuery, allInvoices).Count();
                    var needsAttentionCutoff = DateTime.Now.AddDays(-3);
                    NeedsAttentionInvalidCount = InvoiceNeedsAttentionFilter.Unresolved(invoiceQuery, allInvoices)
                        .Count(i => i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft");
                    NeedsAttentionTransmissionErrorCount = InvoiceNeedsAttentionFilter.Unresolved(invoiceQuery, allInvoices)
                        .Count(i => i.InternalStatusId == "TransmissionError");
                    NeedsAttentionRejectedCount = InvoiceNeedsAttentionFilter.Unresolved(invoiceQuery, allInvoices)
                        .Count(i => i.InternalStatusId == "RequestReject");
                    NeedsAttentionAgingDraftsCount = invoiceQuery.Count(i => i.InternalStatusId == "Draft" && i.CreatedDate <= needsAttentionCutoff);

                    // Apply Date Filters
                    if (FilterType == "Day")
                    {
                        invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Date == FilterDate.Value.Date);
                    }
                    else if (FilterType == "Month")
                    {
                        invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Year == FilterDate.Value.Year && i.CreatedDate.Month == FilterDate.Value.Month);
                    }
                    else if (FilterType == "Year")
                    {
                        invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Year == FilterDate.Value.Year);
                    }

                    // Calculate stats based on the filtered query
                    var invoiceStats = invoiceQuery
                        .GroupBy(i => 1)
                        .Select(g => new
                        {
                            TotalInvoices = g.Count(i => i.InternalStatusId != "Draft"),
                            DraftInvoices = g.Count(i => i.InternalStatusId == "Draft"),

                            RejectRequestInvoices = g.Count(i => i.InternalStatusId == "RequestReject" && i.InternalStatusId != "Draft"),
                            RejectRequestAmount = g.Sum(i => i.InternalStatusId == "RequestReject" && i.InternalStatusId != "Draft" ? (i.TotalAmountIncTax ?? 0m) : 0m),

                            SubmittedInvoices = g.Count(i => i.LHDNStatusId == "Submitted" && i.InternalStatusId != "Draft"),
                            SubmittedAmount = g.Sum(i => i.LHDNStatusId == "Submitted" && i.InternalStatusId != "Draft" ? (i.TotalAmountIncTax ?? 0m) : 0m),

                            ValidInvoices = g.Count(i => i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject" && i.InternalStatusId != "Draft"),
                            TotalValidAmount = g.Sum(i => (i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject" && i.InternalStatusId != "Draft") ? (i.TotalAmountIncTax ?? 0m) : 0m),

                            InvalidInvoices = g.Count(i => i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft"),
                            InvalidAmount = g.Sum(i => i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft" ? (i.TotalAmountIncTax ?? 0m) : 0m),

                            CancelledInvoices = g.Count(i => i.LHDNStatusId == "Cancelled" && i.InternalStatusId != "Draft"),
                            CancelledAmount = g.Sum(i => i.LHDNStatusId == "Cancelled" && i.InternalStatusId != "Draft" ? (i.TotalAmountIncTax ?? 0m) : 0m)
                        })
                        .ToList()
                        .FirstOrDefault();

                    if (invoiceStats != null)
                    {
                        TotalInvoices = invoiceStats.TotalInvoices;
                        DraftInvoices = invoiceStats.DraftInvoices;

                        RejectRequestInvoices = invoiceStats.RejectRequestInvoices;
                        RejectRequestAmount = invoiceStats.RejectRequestAmount;

                        SubmittedInvoices = invoiceStats.SubmittedInvoices;
                        SubmittedAmount = invoiceStats.SubmittedAmount;

                        ValidInvoices = invoiceStats.ValidInvoices;
                        TotalValidAmount = invoiceStats.TotalValidAmount;

                        InvalidInvoices = invoiceStats.InvalidInvoices;
                        InvalidAmount = invoiceStats.InvalidAmount;

                        CancelledInvoices = invoiceStats.CancelledInvoices;
                        CancelledAmount = invoiceStats.CancelledAmount;

                        // "LHDN Invalid – Fix & Resubmit" tile: only invoices still unresolved, so it stops
                        // alerting once the user has resolved or successfully resent them.
                        ActionInvalidCount = InvoiceNeedsAttentionFilter.Unresolved(invoiceQuery, allInvoices)
                            .Count(i => i.LHDNStatusId == "Invalid" && i.InternalStatusId != "Draft");
                    }

                    // Get recent invoices (Smart Sorted for Buyers)
                    if (UserType == "Buyer")
                    {
                        RecentInvoices = _context.InvoiceHeaders.AsNoTracking()
                            .Where(UserExtensions.ReceivedByTins(tins))
                            .Include(i => i.Supplier)
                            // 🔥 Risk Sorting: Bring "RequestReject" and "Invalid" to the very top!
                            .OrderByDescending(i => i.InternalStatusId == "RequestReject" || i.LHDNStatusId == "Invalid")
                            .ThenByDescending(i => i.CreatedDate)
                            .Take(5)
                            .ToList();
                    }
                    else
                    {
                        RecentInvoices = invoiceQuery
                            .Include(i => i.Customer)
                            .Include(i => i.PublicCustomer)
                            .Include(i => i.Supplier)
                            .Where(i => i.InternalStatusId != "Draft")
                            .OrderByDescending(i => i.CreatedDate)
                            .Take(5)
                            .ToList();
                    }

                    // Setup Admin vs Supplier specific stats
                    if (UserType == "Admin")
                    {
                        TotalUsersCount = _context.Users.Count();
                        TotalCompaniesCount = _context.PartyInfos.Count();
                        TotalBuyerCount = _context.Buyers.Count();
                    }
                    else if (UserType == "Supplier")
                    {
                        TotalUsersCount = _context.UserCompanies
                            .Where(uc => companyIds.Contains(uc.PartyInfoId))
                            .Select(uc => uc.UserId)
                            .Distinct()
                            .Count();

                        TotalBuyerCount = _context.SupplierBuyers
                            .Where(sb => companyIds.Contains(sb.SupplierId))
                            .Select(sb => new { sb.BuyerId, sb.PublicCustomerId })
                            .Distinct()
                            .Count();
                    }
                    else if (UserType == "Buyer")
                    {
                        // 1. User & Supplier Counts for Buyer
                        TotalUsersCount = _context.UserCompanies
                            .Where(uc => companyIds.Contains(uc.PartyInfoId))
                            .Select(uc => uc.UserId).Distinct().Count();

                        BuyerTotalSuppliersCount = _context.SupplierBuyers
                            .Where(sb => companyIds.Contains(sb.BuyerId ?? 0))
                            .Select(sb => sb.SupplierId).Distinct().Count();

                        // 2. Received invoices: exactly the Received tab's rule (addressed to this company, LHDN-validated).
                        var buyerInvoices = _context.InvoiceHeaders.AsNoTracking().Where(UserExtensions.ReceivedByTins(tins));

                        // Apply Date Filters
                        if (FilterType == "Day") { buyerInvoices = buyerInvoices.Where(i => i.CreatedDate.Date == FilterDate.Value.Date); }
                        else if (FilterType == "Month") { buyerInvoices = buyerInvoices.Where(i => i.CreatedDate.Year == FilterDate.Value.Year && i.CreatedDate.Month == FilterDate.Value.Month); }
                        else if (FilterType == "Year") { buyerInvoices = buyerInvoices.Where(i => i.CreatedDate.Year == FilterDate.Value.Year); }

                        var t = BuyerTotalsOf(buyerInvoices);
                        BuyerTotalReceivedCount = t.TotalCount;
                        BuyerTotalReceivedAmount = t.TotalAmount;
                        BuyerValidCount = t.ValidCount;
                        BuyerValidAmount = t.ValidAmount;
                        BuyerRequestRejectCount = t.RejectCount;
                        BuyerRequestRejectAmount = t.RejectAmount;
                        BuyerCancelledCount = t.CancelledCount;
                        BuyerCancelledAmount = t.CancelledAmount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching invoice data.");
            }
        
        }


        internal sealed record BuyerTotals(int TotalCount, decimal TotalAmount, int ValidCount, decimal ValidAmount,
            int RejectCount, decimal RejectAmount, int CancelledCount, decimal CancelledAmount);

        /// <summary>Buyer dashboard tiles over received (LHDN-validated) invoices. The three buckets are exclusive, so
        /// Valid + Reject requested + Cancelled = Total: a reject request (internal RequestReject) keeps the document LHDN
        /// Valid, so it is counted as a request, not as Valid; "Rejected" from LHDN (defensive) counts as a request too.</summary>
        internal static BuyerTotals BuyerTotalsOf(IQueryable<InvoiceHeader> received)
        {
            var r = received.GroupBy(i => 1).Select(g => new
            {
                TotalCount = g.Count(),
                TotalAmount = g.Sum(i => i.TotalAmountIncTax ?? 0m),
                ValidCount = g.Count(i => i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject"),
                ValidAmount = g.Sum(i => i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject" ? (i.TotalAmountIncTax ?? 0m) : 0m),
                RejectCount = g.Count(i => (i.LHDNStatusId == "Valid" && i.InternalStatusId == "RequestReject") || i.LHDNStatusId == "Rejected"),
                RejectAmount = g.Sum(i => (i.LHDNStatusId == "Valid" && i.InternalStatusId == "RequestReject") || i.LHDNStatusId == "Rejected" ? (i.TotalAmountIncTax ?? 0m) : 0m),
                CancelledCount = g.Count(i => i.LHDNStatusId == "Cancelled"),
                CancelledAmount = g.Sum(i => i.LHDNStatusId == "Cancelled" ? (i.TotalAmountIncTax ?? 0m) : 0m),
            }).FirstOrDefault();
            return r == null ? new BuyerTotals(0, 0, 0, 0, 0, 0, 0, 0)
                : new BuyerTotals(r.TotalCount, r.TotalAmount, r.ValidCount, r.ValidAmount, r.RejectCount, r.RejectAmount, r.CancelledCount, r.CancelledAmount);
        }

        public JsonResult OnGetInvoiceData(string FilterType = "Month", DateTime? FilterDate = null, string? FilterStatus = null, string? FilterSupplier = null)
        {
            try
            {
                // Default filter date to today if not provided
                if (!FilterDate.HasValue) FilterDate = DateTime.Now;

                var user = User;
                var companyIds = user.GetUserCompanyIds(_context);
                if (!companyIds.Any())
                    return new JsonResult(new { error = "No companies linked to user." });

                var tins = UserExtensions.TinsOf(_context, companyIds);
                var invoiceQuery = _context.InvoiceHeaders
                    .Include(i => i.Customer)
                    .Where(i => i.InternalStatusId != "Draft")
                    .Where(UserExtensions.VisibleToTins(tins))
                    .AsNoTracking();

                // ✅ Apply the exact same Date Filters so the charts match the top cards
                if (FilterType == "Day")
                {
                    invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Date == FilterDate.Value.Date);
                }
                else if (FilterType == "Month")
                {
                    invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Year == FilterDate.Value.Year && i.CreatedDate.Month == FilterDate.Value.Month);
                }
                else if (FilterType == "Year")
                {
                    invoiceQuery = invoiceQuery.Where(i => i.CreatedDate.Year == FilterDate.Value.Year);
                }
                if (!string.IsNullOrEmpty(FilterStatus))
                {
                    invoiceQuery = invoiceQuery.Where(i => i.LHDNStatusId == FilterStatus || i.InternalStatusId == FilterStatus);
                }
                if (!string.IsNullOrEmpty(FilterSupplier))
                {
                    invoiceQuery = invoiceQuery.Where(i => i.Supplier != null && i.Supplier.CompanyName.Contains(FilterSupplier));
                }

                // Execute query
                var invoices = invoiceQuery.ToList();

                var docTypeNames = DocumentTypeDisplay.GetDisplayNames();
                _logger.LogInformation("User.Identity.Name: {UserIdentity}", User.Identity?.Name ?? "NULL");
                _logger.LogInformation("Filtered invoices count: {InvoiceCount}", invoices.Count);

                var customerRevenueShare = invoices
                    .Where(i => i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject" && i.InternalStatusId != "Draft")
                    .Where(i => i.Customer != null || i.PublicCustomer != null)
                    .GroupBy(i => i.Customer != null ? i.Customer.CompanyName : i.PublicCustomer!.CompanyName)
                    .Select(g => new
                    {
                        CustomerName = g.Key ?? "Unknown Buyer",
                        TotalAmount = g.Sum(i => i.TotalAmountIncTax ?? 0m)
                    })

                    .Where(c => c.TotalAmount > 0 && c.CustomerName != "NA" && c.CustomerName != "Unknown Buyer" && !string.IsNullOrWhiteSpace(c.CustomerName))
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(5)
                    .ToList();

                var supplierSpendShare = invoices
                    .Where(i => i.LHDNStatusId == "Valid" && i.InternalStatusId != "RequestReject" && i.InternalStatusId != "Draft")
                    .Where(i => i.Supplier != null)
                    .GroupBy(i => i.Supplier.CompanyName)
                    .Select(g => new
                    {
                        SupplierName = g.Key ?? "Unknown Supplier",
                        TotalAmount = g.Sum(i => i.TotalAmountIncTax ?? 0m)
                    })
                    .Where(c => c.TotalAmount > 0 && c.SupplierName != "NA" && !string.IsNullOrWhiteSpace(c.SupplierName))
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(5)
                    .ToList();

                var supplierRejectionRates = invoices
                    .Where(i => i.Supplier != null)
                    .GroupBy(i => i.Supplier.CompanyName)
                    .Select(g => new
                    {
                        SupplierName = g.Key ?? "Unknown Supplier",
                        TotalInvoices = g.Count(),
                        RejectedCount = g.Count(i => i.InternalStatusId == "RequestReject" || i.LHDNStatusId == "Invalid")
                    })
                    .Where(c => c.RejectedCount > 0)
                    .Select(c => new
                    {
                        c.SupplierName,
                        c.TotalInvoices,
                        c.RejectedCount,
                        RejectionRate = Math.Round((double)c.RejectedCount / c.TotalInvoices * 100, 1)
                    })
                    .OrderByDescending(c => c.RejectionRate)
                    .ThenByDescending(c => c.RejectedCount)
                    .Take(5)
                    .ToList();

                var predefinedReasons = CancelDocumentInput.GetReasons();

                // Internal Rejections (RequestReject)
                var internalRejectedReasons = invoices
                    .Where(i => i.InternalStatusId == "RequestReject")
                    .GroupBy(i => i.RejectedReason != null && predefinedReasons.Contains(i.RejectedReason) ? i.RejectedReason : "Others")
                    .Select(g => new { Reason = g.Key, Count = g.Count() })
                    .ToList();

                var completeInternalRejected = predefinedReasons
                    .Select(reason => new {
                        Reason = reason,
                        Count = internalRejectedReasons.FirstOrDefault(r => r.Reason == reason)?.Count ?? 0,
                        SuggestedFix = "Internal rejection requested by buyer. Please clarify with them directly." // Don't use mapper here
                    })
                    .ToList();

                if (!completeInternalRejected.Any(r => r.Reason == "Others"))
                {
                    completeInternalRejected.Add(new { Reason = "Others", Count = internalRejectedReasons.FirstOrDefault(r => r.Reason == "Others")?.Count ?? 0, SuggestedFix = "Internal rejection requested by buyer. Please clarify with them directly." });
                }

                //  LHDN Cancellations
                var cancelledReasons = invoices
                    .Where(i => i.LHDNStatusId == "Cancelled")
                    .GroupBy(i => i.RejectedReason != null && predefinedReasons.Contains(i.RejectedReason) ? i.RejectedReason : "Others")
                    .Select(g => new { Reason = g.Key, Count = g.Count() })
                    .ToList();

                var completeCancelled = predefinedReasons
                    .Select(reason => new {
                        Reason = reason,
                        Count = cancelledReasons.FirstOrDefault(r => r.Reason == reason)?.Count ?? 0,
                        SuggestedFix = EINVWORLD.Helpers.RejectionFixMapper.GetFixSuggestion(reason) // Use mapper here!
                    })
                    .ToList();

                if (!completeCancelled.Any(r => r.Reason == "Others"))
                {
                    completeCancelled.Add(new { Reason = "Others", Count = cancelledReasons.FirstOrDefault(r => r.Reason == "Others")?.Count ?? 0, SuggestedFix = EINVWORLD.Helpers.RejectionFixMapper.GetFixSuggestion("Others") });
                }

                var customerRejectionRates = invoices
                    .Where(i => i.Customer != null || i.PublicCustomer != null)
                    .GroupBy(i => i.Customer != null ? i.Customer.CompanyName : i.PublicCustomer!.CompanyName)
                    .Select(g => new
                    {
                        CustomerName = g.Key ?? "Unknown Buyer",
                        TotalInvoices = g.Count(),

                        // 🔥 THE FIX: ONLY count invoices actually rejected by the buyer!
                        // We removed "Cancelled" so your own cancellations don't inflate the buyer's failure rate.
                        RejectedCount = g.Count(i => i.InternalStatusId == "RequestReject")
                    })
                    .Where(c => c.RejectedCount > 0)
                    .Select(c => new
                    {
                        c.CustomerName,
                        c.TotalInvoices,
                        c.RejectedCount,
                        RejectionRate = Math.Round((double)c.RejectedCount / c.TotalInvoices * 100, 1)
                    })
                    .OrderByDescending(c => c.RejectionRate)
                    .ThenByDescending(c => c.RejectedCount)
                    .Take(5)
                    .ToList();

                // Staff usernames only from invoices this company issued — never the counterparty's staff.
                var issuedNos = invoiceQuery.Where(UserExtensions.IssuedByCompanies(companyIds)).Select(i => i.InvoiceNo).ToHashSet();
                var internalErrorRates = invoices
                    .Where(i => issuedNos.Contains(i.InvoiceNo) && !string.IsNullOrEmpty(i.CreatedBy))
                    .GroupBy(i => i.CreatedBy)
                    .Select(g => new
                    {
                        StaffName = g.Key,
                        TotalCreated = g.Count(),
                        // Count how many of their invoices ended up being cancelled
                        CancelledCount = g.Count(i => i.LHDNStatusId == "Cancelled" || i.InternalStatusId == "Cancelled")
                    })
                    .Where(c => c.CancelledCount > 0)
                    .Select(c => new
                    {
                        c.StaffName,
                        c.TotalCreated,
                        c.CancelledCount,
                        ErrorRate = Math.Round((double)c.CancelledCount / c.TotalCreated * 100, 1)
                    })
                    .OrderByDescending(c => c.ErrorRate)
                    .ThenByDescending(c => c.CancelledCount)
                    .Take(5)
                    .ToList();

                var threeDaysAgo = DateTime.Now.AddDays(-3);

                var agingDrafts = _context.InvoiceHeaders
                    .Include(i => i.Customer)
                    .Include(i => i.PublicCustomer)
                    .Where(i => i.InternalStatusId == "Draft" &&
                                i.CreatedDate <= threeDaysAgo) // Only grab drafts older than 3 days
                    .Where(UserExtensions.IssuedByTins(tins)) // drafts are the issuer's alone
                    .AsNoTracking()
                    .Select(i => new
                    {
                        i.InvoiceNo,
                        CustomerName = i.Customer != null ? i.Customer.CompanyName : (i.PublicCustomer != null ? i.PublicCustomer.CompanyName : "Unknown Buyer"),
                        i.CreatedDate
                    })
                    .ToList() // Pull into memory safely before doing exact day calculations
                    .Select(d => new
                    {
                        d.InvoiceNo,
                        d.CustomerName,
                        CreatedDate = d.CreatedDate.ToString("dd MMM yyyy"),
                        // Calculate exactly how many days it has been stuck
                        DaysStuck = (DateTime.Now - d.CreatedDate).Days
                    })
                    .OrderByDescending(d => d.DaysStuck) // Put the oldest, most critical drafts at the top!
                    .Take(5) // Show the top 5 worst offenders
                    .ToList();
                return new JsonResult(new
                {
                    StatusCounts = invoices.GroupBy(i => i.InternalStatusId ?? "Unknown")
                        .Select(g => new { Status = g.Key, Count = g.Count() }),

                    AmountByMonth = invoices.Where(i => i.IssueDate.HasValue)
                        .GroupBy(i => i.IssueDate!.Value.ToString("yyyy-MM"))
                        .Select(g => new { Month = g.Key, Total = g.Sum(i => i.TotalAmountIncTax ?? 0) }),

                    InvoicesByCustomer = customerRevenueShare,

                    // Send the separated lists to the frontend
                    InternalRejectedReasons = completeInternalRejected,
                    CancelledReasons = completeCancelled,
                    CustomerRejectionRates = customerRejectionRates,
                    InternalErrorRates = internalErrorRates,
                    AgingDrafts = agingDrafts,
                    SupplierSpendShare = supplierSpendShare, 
                    SupplierRejectionRates = supplierRejectionRates,

                    InvoiceType = Enum.GetValues(typeof(eInvWorld.Models.InputModel.EInvoiceDocumentType))
                        .Cast<eInvWorld.Models.InputModel.EInvoiceDocumentType>()
                        .Select(type => new
                        {
                            Type = docTypeNames.TryGetValue(type, out string? displayName) ? displayName : type.ToString(),
                            Count = invoices.Count(i => int.TryParse(i.DocTypeCode, out int code) && code == (int)type)
                        })
                        .ToList(),

                    MonthlyInvoiceTypes = invoices
                        .GroupBy(i => new { Month = i.IssueDate!.Value.ToString("yyyy-MM"), Type = i.DocTypeCode })
                        .Select(g => new
                        {
                            Months = g.Key.Month,
                            Type = int.TryParse(g.Key.Type, out int typeCode) && Enum.IsDefined(typeof(eInvWorld.Models.InputModel.EInvoiceDocumentType), typeCode)
                                ? docTypeNames.TryGetValue((eInvWorld.Models.InputModel.EInvoiceDocumentType)typeCode, out string? displayName) ? displayName : g.Key.Type
                                : "Unknown Type",
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
