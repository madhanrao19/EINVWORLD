using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Admin.Logs
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<SystemLog> Logs { get; set; } = new List<SystemLog>();

        // Pagination Properties
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 50;
        public int TotalLogs { get; set; }

        // Filter Properties (SupportsGet = true allows passing them in the URL)
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? LogLevelFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        public async Task OnGetAsync(int pageIndex = 1)
        {
            // 1. Start with the base query. SystemLogs is owned by the Serilog sink (not an EF entity),
            //    so we read it via a raw SQL source; LINQ filters/sort/paging below compose onto it.
            IQueryable<SystemLog> query = _context.Database.SqlQueryRaw<SystemLog>(
                "SELECT Id, Message, Level, TimeStamp, Exception, LogEvent, IPAddress, UserName FROM SystemLogs");

            // 2. Apply Filters
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                // Search in Message, UserName, or IP
                query = query.Where(l =>
                    (l.Message != null && l.Message.Contains(SearchTerm)) ||
                    (l.UserName != null && l.UserName.Contains(SearchTerm)) ||
                    (l.IPAddress != null && l.IPAddress.Contains(SearchTerm)));
            }

            if (!string.IsNullOrEmpty(LogLevelFilter))
            {
                query = query.Where(l => l.Level == LogLevelFilter);
            }

            if (StartDate.HasValue)
            {
                query = query.Where(l => l.TimeStamp >= StartDate.Value);
            }

            if (EndDate.HasValue)
            {
                // Add 1 day to include the entire end date (e.g. 23:59:59)
                var nextDay = EndDate.Value.AddDays(1);
                query = query.Where(l => l.TimeStamp < nextDay);
            }

            // 3. Get Total Count (based on filters)
            TotalLogs = await query.CountAsync();

            // 4. Calculate Total Pages
            TotalPages = (int)Math.Ceiling(TotalLogs / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            // Clamp Page Index
            if (pageIndex < 1) pageIndex = 1;
            if (pageIndex > TotalPages) pageIndex = TotalPages;

            CurrentPage = pageIndex;

            // 5. Get Specific Page Data
            Logs = await query
                .OrderByDescending(l => l.TimeStamp)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostClearAllAsync()
        {
            // Note: This still clears everything, ignoring filters (which is usually intended behavior for "Clear All")
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM [SystemLogs]");
            return RedirectToPage();
        }
    }
}