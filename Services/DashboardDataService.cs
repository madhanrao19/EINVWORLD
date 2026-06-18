using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Services
{
    public class DashboardDataService
    {
        private readonly ApplicationDbContext _context;

        public DashboardDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ChartDataPoint>> GetTopProductsAsync(int? supplierId, int? customerId, int year, string currency)
        {
            var query = _context.InvoiceLines
                .Include(x => x.InvoiceHeader)
                .Where(x =>
                    x.InvoiceHeader != null &&
                    x.InvoiceHeader.InternalStatusId != "Draft" &&
                    x.InvoiceHeader.IssueDate.HasValue &&
                    x.InvoiceHeader.IssueDate.Value.Year == year &&
                    x.InvoiceHeader.Currency == currency);

            if (supplierId.HasValue)
                query = query.Where(x => x.InvoiceHeader.SupplierId == supplierId);

            if (customerId.HasValue)
                query = query.Where(x => x.InvoiceHeader.CustomerId == customerId);

            var result = await query
                .GroupBy(x => x.ItemCode ?? "Unknown")
                 .Select(g => new ChartDataPoint
                 {
                     Label = g.Key,
                     Value = g.Count()
                 })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToListAsync();

            return result;
        }

        public async Task<List<ChartDataPoint>> GetRejectedReasonsAsync(int? supplierId, int? customerId, int year, string currency)
        {
            var query = _context.InvoiceHeaders
                .AsNoTracking()
                .Where(i =>
                    i.LHDNStatusId == "Rejected" &&
                    i.IssueDate.HasValue &&
                    i.IssueDate.Value.Year == year &&
                    i.Currency == currency);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId);

            var result = await query
                .GroupBy(i => i.RejectedReason ?? "Unknown")
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync();

            return result;
        }

        public async Task<List<ChartDataPoint>> GetInvoicesByCustomerAsync(int? supplierId, int? customerId, int year, string currency)
        {
            var query = _context.InvoiceHeaders
                .Include(i => i.Customer)
                .Where(i =>
                    i.InternalStatusId != "Draft" &&
                    i.IssueDate.HasValue &&
                    i.IssueDate.Value.Year == year &&
                    i.Currency == currency);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId);

            var result = await query
                .GroupBy(i => i.Customer != null ? i.Customer.CompanyName : "Unknown")
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync();

            return result;
        }

        public async Task<List<ChartDataPoint>> GetInvoiceTypesAsync(int? supplierId, int? customerId, int year, string currency)
        {
            var query = _context.InvoiceHeaders
                .Where(i =>
                    i.InternalStatusId != "Draft" &&
                    i.IssueDate.HasValue &&
                    i.IssueDate.Value.Year == year &&
                    i.Currency == currency);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId);

            var result = await query
                .GroupBy(i => i.DocTypeCode)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToListAsync();

            return result;
        }

        public async Task<List<ChartDataPoint>> GetMonthlySummaryAsync(int? supplierId, int? customerId, int year, string currency)
        {
            var query = _context.InvoiceHeaders
                .AsNoTracking()
                .Where(i =>
                    i.IssueDate.HasValue &&
                    i.IssueDate.Value.Year == year &&
                    i.Currency == currency &&
                    i.InternalStatusId != "Draft");

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId);

            var monthlyData = await query.ToListAsync();

            var result = monthlyData
                .GroupBy(i => i.IssueDate!.Value.Month)
                .Select(g => new ChartDataPoint
                {
                    Label = new DateTime(year, g.Key, 1).ToString("MMM"),
                    Value = g.Count()
                })
                .OrderBy(g => g.Label)
                .ToList();

            return result;
        }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = "";
        public int Value { get; set; }
    }
}
