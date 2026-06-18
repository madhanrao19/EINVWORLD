using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.MSICSubCategoryCodes
{
    public class ListMSICSubCategoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListMSICSubCategoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<MSICSubCategoryCode> MSICSubCategories { get; set; } = new List<MSICSubCategoryCode>();

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
            var query = _context.MSICSubCategoryCodes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(m => m.Code.Contains(SearchTerm) || m.Description.Contains(SearchTerm) || m.MSICCategoryReference.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(m => m.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(m => !m.IsActive);
            }
            else query = query.Where(m => m.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "description": query = isDesc ? query.OrderByDescending(m => m.Description) : query.OrderBy(m => m.Description); break;
                case "reference": query = isDesc ? query.OrderByDescending(m => m.MSICCategoryReference) : query.OrderBy(m => m.MSICCategoryReference); break;
                default: query = isDesc ? query.OrderByDescending(m => m.Code) : query.OrderBy(m => m.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            MSICSubCategories = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}