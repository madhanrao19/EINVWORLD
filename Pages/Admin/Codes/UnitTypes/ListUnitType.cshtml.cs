using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.UnitTypes
{
    public class ListUnitTypeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListUnitTypeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<UnitType> UnitTypes { get; set; } = new List<UnitType>();

        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? SortBy { get; set; }
        [BindProperty(SupportsGet = true)] public string? SortOrder { get; set; }

        // Pagination Properties
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 25;

        public void OnGet()
        {
            var query = _context.UnitTypes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(u => u.Code.Contains(SearchTerm) || u.Name.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(u => u.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(u => !u.IsActive);
            }
            else query = query.Where(u => u.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "name": query = isDesc ? query.OrderByDescending(u => u.Name) : query.OrderBy(u => u.Name); break;
                default: query = isDesc ? query.OrderByDescending(u => u.Code) : query.OrderBy(u => u.Code); break;
            }

            // Pagination Logic
            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            UnitTypes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}