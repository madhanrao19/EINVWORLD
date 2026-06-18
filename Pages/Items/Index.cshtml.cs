using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Items
{
    [Authorize(Roles = "Admin,Supplier")]
    public class IndexModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public class ItemViewModel
        {
            public ItemDescription Item { get; set; } = default!;
            public string CompanyName { get; set; } = string.Empty;
        }

        public IList<ItemViewModel> ItemViewModels { get; set; } = default!;
        public bool IsAdmin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortBy { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        // ✅ ADDED: Pagination Properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 10; // Change this to show more/less per page

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IsAdmin = User.IsInRole("Admin");

            var query = from item in _context.ItemDescriptions
                        join party in _context.PartyInfos
                            on item.CreatedByCompanyId equals party.PartyInfoId into itemParty
                        from party in itemParty.DefaultIfEmpty()
                        select new ItemViewModel
                        {
                            Item = item,
                            CompanyName = party != null ? party.CompanyName : "-"
                        };

            if (!IsAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany != null)
                {
                    query = query.Where(i => i.Item.CreatedByCompanyId == userCompany.PartyInfoId);
                }
                else
                {
                    query = query.Where(i => false);
                }
            }

            // --- Apply Filtering Logic ---
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(q =>
                    q.Item.ItemCode.Contains(SearchTerm) ||
                    q.Item.Description.Contains(SearchTerm) ||
                    q.Item.ClassificationCode.Contains(SearchTerm) ||
                    q.CompanyName.Contains(SearchTerm));
            }

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active")
                {
                    query = query.Where(q => q.Item.IsActive);
                }
                else if (StatusFilter == "Inactive")
                {
                    query = query.Where(q => !q.Item.IsActive);
                }
            }
            // -----------------------------

            // --- Apply Sorting Logic ---
            bool isDesc = SortOrder == "desc";

            switch (SortBy?.ToLower())
            {
                case "itemcode":
                    query = isDesc ? query.OrderByDescending(q => q.Item.ItemCode) : query.OrderBy(q => q.Item.ItemCode);
                    break;
                case "classification":
                    query = isDesc ? query.OrderByDescending(q => q.Item.ClassificationCode) : query.OrderBy(q => q.Item.ClassificationCode);
                    break;
                case "description":
                    query = isDesc ? query.OrderByDescending(q => q.Item.Description) : query.OrderBy(q => q.Item.Description);
                    break;
                case "company":
                    query = isDesc ? query.OrderByDescending(q => q.CompanyName) : query.OrderBy(q => q.CompanyName);
                    break;
                case "updated":
                    query = isDesc ? query.OrderByDescending(q => q.Item.UpdatedDate) : query.OrderBy(q => q.Item.UpdatedDate);
                    break;
                default:
                    query = query.OrderBy(q => q.Item.ItemCode);
                    break;
            }
            // ---------------------------

            // ✅ Apply Pagination Logic
            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            ItemViewModels = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}