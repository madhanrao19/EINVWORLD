using eInvWorld.Data;
using eInvWorld.Models.Audit;

namespace eInvWorld.Services.Logging
{
    public class InvoiceHistoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public InvoiceHistoryService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public void Log(string invoiceNo, string action, string? remarks = null)
        {
            var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";

            var history = new InvoiceHistory
            {
                InvoiceNo = invoiceNo,
                Action = action,
                PerformedBy = username,
                Timestamp = DateTime.UtcNow,
                Remarks = remarks
            };

            _context.InvoiceHistories.Add(history);
            _context.SaveChanges();
        }
    }
}
