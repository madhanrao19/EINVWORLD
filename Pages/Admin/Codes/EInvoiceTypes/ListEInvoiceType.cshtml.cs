using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.EInvoiceTypes
{
    public class ListEInvoiceTypeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListEInvoiceTypeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<EInvoiceType> EInvoiceTypes { get; set; } = new List<EInvoiceType>();

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
            var query = _context.EInvoiceTypes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(e => e.Code.Contains(SearchTerm) || e.Description.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(e => e.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(e => !e.IsActive);
            }
            else query = query.Where(e => e.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "description": query = isDesc ? query.OrderByDescending(e => e.Description) : query.OrderBy(e => e.Description); break;
                default: query = isDesc ? query.OrderByDescending(e => e.Code) : query.OrderBy(e => e.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            EInvoiceTypes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}