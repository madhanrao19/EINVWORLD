using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.TaxTypes
{
    public class ListTaxTypeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListTaxTypeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<TaxType> TaxTypes { get; set; } = new List<TaxType>();

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
            var query = _context.TaxTypes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(t => t.Code.Contains(SearchTerm) || t.Description.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(t => t.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(t => !t.IsActive);
            }
            else query = query.Where(t => t.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "description": query = isDesc ? query.OrderByDescending(t => t.Description) : query.OrderBy(t => t.Description); break;
                default: query = isDesc ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            TaxTypes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}