using eInvWorld.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Pages.Dashboard
{
    public class MainDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MainDashboardModel> _logger;

        public MainDashboardModel(ApplicationDbContext context, ILogger<MainDashboardModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public JsonResult OnGetChartData()
        {
            try
            {
                var invoices = _context.InvoiceHeaders
                    .Include(i => i.Customer)
                    .Where(i => i.InternalStatusId != "Draft" && i.IssueDate.HasValue)
                    .AsNoTracking()
                    .ToList();

                var result = new
                {
                    TopProducts = invoices
                        .GroupBy(i => i.Customer?.CompanyName ?? "Unknown")
                        .Select(g => new { label = g.Key, value = g.Count() })
                        .ToList(),

                    RejectedReasons = invoices
                        .Where(i => i.LHDNStatusId == "Rejected")
                        .GroupBy(i => i.RejectedReason ?? "Others")
                        .Select(g => new { label = g.Key, value = g.Count() })
                        .ToList(),

                    InvoiceTypes = invoices
                        .GroupBy(i => i.DocTypeCode ?? "Unknown")
                        .Select(g => new { label = g.Key, value = g.Count() })
                        .ToList(),

                    Monthly = invoices
                        .GroupBy(i => i.IssueDate!.Value.ToString("MMM"))
                        .Select(g => new { label = g.Key, value = g.Count() })
                        .ToList()
                };

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard data.");
                return new JsonResult(new { error = "Failed to load dashboard data.", exception = ex.Message });
            }
        }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
