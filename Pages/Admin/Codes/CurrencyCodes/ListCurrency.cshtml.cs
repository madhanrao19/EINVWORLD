using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.CurrencyCodes
{
    public class ListCurrencyModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListCurrencyModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<CurrencyCode> CurrencyCodes { get; set; } = new List<CurrencyCode>();

        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? SortBy { get; set; }
        [BindProperty(SupportsGet = true)] public string? SortOrder { get; set; }

        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 25;

        public void OnGet()
        {
            var query = _context.CurrencyCodes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(c => c.Code.Contains(SearchTerm) || c.Currency.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(c => c.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(c => !c.IsActive);
            }
            else query = query.Where(c => c.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "currency": query = isDesc ? query.OrderByDescending(c => c.Currency) : query.OrderBy(c => c.Currency); break;
                default: query = isDesc ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            CurrencyCodes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}