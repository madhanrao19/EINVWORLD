using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ApplicationDbContext context, ILogger<IndexModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IList<PartyInfo> PartyInfo { get; set; } = new List<PartyInfo>();

        [BindProperty(SupportsGet = true)]
        public string? SearchName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTIN { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchEmail { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortBy { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        // Pagination Properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }

        public IList<StateCode> StateCodes { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // Query PartyInfo model directly (not Supplier)
            IQueryable<PartyInfo> partyInfoQuery = _context.PartyInfos
                .Where(p => p.IsApproved); // Only approved companies;

            // Filter PartyInfo based on the search strings
            if (!string.IsNullOrEmpty(SearchName))
            {
                partyInfoQuery = partyInfoQuery.Where(p => p.CompanyName.Contains(SearchName));
            }

            if (!string.IsNullOrEmpty(SearchTIN))
            {
                partyInfoQuery = partyInfoQuery.Where(p => p.TIN.Contains(SearchTIN));
            }

            if (!string.IsNullOrEmpty(SearchEmail))
            {
                partyInfoQuery = partyInfoQuery.Where(p => p.Email != null && p.Email.Contains(SearchEmail));
            }

            // Apply Sorting
            SortBy = string.IsNullOrEmpty(SortBy) ? "name" : SortBy.ToLower();
            SortOrder = string.IsNullOrEmpty(SortOrder) ? "asc" : SortOrder.ToLower();

            partyInfoQuery = SortBy switch
            {
                "tin" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.TIN) : partyInfoQuery.OrderByDescending(p => p.TIN),
                "regtype" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.RegTypeCode) : partyInfoQuery.OrderByDescending(p => p.RegTypeCode),
                "email" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.Email) : partyInfoQuery.OrderByDescending(p => p.Email),
                "updatedby" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.UpdatedBy) : partyInfoQuery.OrderByDescending(p => p.UpdatedBy),
                "updateddate" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.UpdatedDate) : partyInfoQuery.OrderByDescending(p => p.UpdatedDate),
                "isactive" => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.IsActive) : partyInfoQuery.OrderByDescending(p => p.IsActive),
                _ => SortOrder == "asc" ? partyInfoQuery.OrderBy(p => p.CompanyName) : partyInfoQuery.OrderByDescending(p => p.CompanyName),
            };

            // Apply Pagination
            TotalRecords = await partyInfoQuery.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            PartyInfo = await partyInfoQuery
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Load the state codes (if needed)
            StateCodes = await _context.StateCodes.ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync([FromQuery] int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid delete request received with no ID.");
                return BadRequest();
            }

            var company = await _context.PartyInfos.FindAsync(id);
            if (company == null)
            {
                _logger.LogWarning($"Supplier with ID {id} not found.");
                return NotFound();
            }

            company.IsAdminCreated = true;
            company.IsApproved = User.IsInRole("Admin");

            _context.PartyInfos.Remove(company);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Company with ID {id} deleted successfully.");

            return new JsonResult(new { success = true, message = "Supplier deleted successfully!" });
        }

        // Method to get state name by state code
        public string GetStateName(string stateCode)
        {
            return StateCodes.FirstOrDefault(s => s.Code == stateCode)?.State ?? "Unknown";
        }
    }
}