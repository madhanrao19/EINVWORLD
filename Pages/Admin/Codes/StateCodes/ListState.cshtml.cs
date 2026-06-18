using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.StateCodes
{
    public class ListStateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListStateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<StateCode> StateCodes { get; set; } = new List<StateCode>();

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
            var query = _context.StateCodes.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(s => s.Code.Contains(SearchTerm) || s.State.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(s => s.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(s => !s.IsActive);
            }
            else query = query.Where(s => s.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "state": query = isDesc ? query.OrderByDescending(s => s.State) : query.OrderBy(s => s.State); break;
                default: query = isDesc ? query.OrderByDescending(s => s.Code) : query.OrderBy(s => s.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            StateCodes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}