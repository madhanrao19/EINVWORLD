using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.CountryCodes
{
    public class ListCountryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListCountryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<CountryCode> CountryCodes { get; set; } = new List<CountryCode>();

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
            var query = _context.CountryCodes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(c => c.Code.Contains(SearchTerm) || c.Country.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(c => c.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(c => !c.IsActive);
            }
            else query = query.Where(c => c.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "country": query = isDesc ? query.OrderByDescending(c => c.Country) : query.OrderBy(c => c.Country); break;
                default: query = isDesc ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            CountryCodes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}