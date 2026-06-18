using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eInvWorld.Pages.Admin.Codes.PaymentModes
{
    public class ListPaymentModeModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ListPaymentModeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<PaymentMode> PaymentModes { get; set; } = new List<PaymentMode>();

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
            var query = _context.PaymentMethods.AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
                query = query.Where(p => p.Code.Contains(SearchTerm) || p.PaymentMethod.Contains(SearchTerm));

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active") query = query.Where(p => p.IsActive);
                else if (StatusFilter == "Inactive") query = query.Where(p => !p.IsActive);
            }
            else query = query.Where(p => p.IsActive);

            bool isDesc = SortOrder == "desc";
            switch (SortBy?.ToLower())
            {
                case "method": query = isDesc ? query.OrderByDescending(p => p.PaymentMethod) : query.OrderBy(p => p.PaymentMethod); break;
                default: query = isDesc ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code); break;
            }

            TotalRecords = query.Count();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            PaymentModes = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }
    }
}