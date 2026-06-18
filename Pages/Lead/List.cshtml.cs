using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Lead
{
    [Authorize(Roles = "Admin")]
    public class ListModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<PartyInfo> LeadCompanies { get; set; } = new List<PartyInfo>();
        public List<int> AssignedBuyerIds { get; set; } = new();
        public int? CurrentUserSupplierId { get; set; }

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

        public async Task OnGetAsync()
        {
            var query = _context.PartyInfos
                .Include(p => p.Country)
                .Include(p => p.RegType)
                .Include(p => p.State)
                .AsQueryable();

            if (User.IsInRole("Admin"))
            {
                // Admin Logic: Only see Pending approvals
                query = query.Where(p => !p.IsApproved);
            }
            else
            {
                // User Logic: See ALL records created by me (Pending + Approved)
                var currentUserEmail = User.Identity?.Name;
                query = query.Where(p => p.CreatedBy == currentUserEmail);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
                if (user != null)
                {
                    var userCompany = await _context.UserCompanies
                        .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.IsPrimaryCompany)
                        ?? await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.UserId == user.Id);

                    if (userCompany != null)
                    {
                        CurrentUserSupplierId = userCompany.PartyInfoId;
                        AssignedBuyerIds = await _context.SupplierBuyers
                            .Where(sb => sb.SupplierId == CurrentUserSupplierId && sb.BuyerId != null)
                            .Select(sb => sb.BuyerId!.Value)
                            .ToListAsync();
                    }
                }
            }

            // Apply Search Filters
            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(p => p.CompanyName.Contains(SearchName));

            if (!string.IsNullOrWhiteSpace(SearchTIN))
                query = query.Where(p => p.TIN.Contains(SearchTIN));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(p => p.Email != null && p.Email.Contains(SearchEmail));

            // Apply Sorting
            SortBy = string.IsNullOrEmpty(SortBy) ? "name" : SortBy.ToLower();
            SortOrder = string.IsNullOrEmpty(SortOrder) ? "asc" : SortOrder.ToLower();

            query = SortBy switch
            {
                "tin" => SortOrder == "asc" ? query.OrderBy(p => p.TIN) : query.OrderByDescending(p => p.TIN),
                "regtype" => SortOrder == "asc" ? query.OrderBy(p => p.RegTypeCode) : query.OrderByDescending(p => p.RegTypeCode),
                "email" => SortOrder == "asc" ? query.OrderBy(p => p.Email) : query.OrderByDescending(p => p.Email),
                "updatedby" => SortOrder == "asc" ? query.OrderBy(p => p.UpdatedBy) : query.OrderByDescending(p => p.UpdatedBy),
                "updateddate" => SortOrder == "asc" ? query.OrderBy(p => p.UpdatedDate) : query.OrderByDescending(p => p.UpdatedDate),
                _ => SortOrder == "asc" ? query.OrderBy(p => p.CompanyName) : query.OrderByDescending(p => p.CompanyName),
            };

            // Apply Pagination
            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            LeadCompanies = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var party = await _context.PartyInfos.FindAsync(id);
            if (party == null)
                return NotFound();

            party.IsApproved = true;
            party.UpdatedDate = DateTime.Now;
            party.UpdatedBy = User.Identity?.Name ?? "System";

            _context.PartyInfos.Update(party);

            _context.UserActivityLogs.Add(new UserActivityLog
            {
                UserId = User.Identity?.Name ?? "System",
                UserName = User.Identity?.Name ?? "System",
                Action = "Approved Company Lead",
                Module = "PublicCustomer",
                Data = $"Approved: {party.CompanyName} ({party.TIN})",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostToggleAssignAsync(int buyerId)
        {
            var currentUserEmail = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
            if (user == null) return new JsonResult(new { success = false, message = "User not found" });

            var userCompany = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.IsPrimaryCompany)
                ?? await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.UserId == user.Id);

            if (userCompany == null) return new JsonResult(new { success = false, message = "You do not have a supplier company." });

            int supplierId = userCompany.PartyInfoId;

            var existingAssignment = await _context.SupplierBuyers
                .FirstOrDefaultAsync(sb => sb.SupplierId == supplierId && sb.BuyerId == buyerId);

            bool isAssigned = false;

            if (existingAssignment != null)
            {
                _context.SupplierBuyers.Remove(existingAssignment);
                isAssigned = false;
            }
            else
            {
                _context.SupplierBuyers.Add(new SupplierBuyer { SupplierId = supplierId, BuyerId = buyerId });
                isAssigned = true;
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, isAssigned = isAssigned });
        }
    }
}