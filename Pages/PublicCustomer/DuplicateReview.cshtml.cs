using eInvWorld.Data;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace eInvWorld.Pages.PublicCustomer
{
    // ponytail: display-only in this pass — merge/delete actions are a separate, higher-risk
    // change (needs an audit trail + FK re-pointing) called out during the buyer-management review.
    [Authorize(Roles = "Admin,Supplier")]
    public class DuplicateReviewModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;

        public DuplicateReviewModel(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public class DuplicateGroup
        {
            public string Reason { get; set; } = "";
            public string MatchValue { get; set; } = "";
            public List<eInvWorld.Models.InputModel.PublicCustomer> Buyers { get; set; } = new();
        }

        public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
        public Dictionary<string, string> StateNames { get; set; } = new();

        public async Task OnGetAsync()
        {
            bool isAdmin = User.IsInRole("Admin");

            StateNames = await _context.StateCodes.ToDictionaryAsync(s => s.Code, s => s.State);

            // Same tenant-scoping pattern as Import.cshtml.cs OnPostUploadAsync: a Supplier only ever
            // sees duplicates within their own buyers; an Admin only sees duplicates among global
            // (Admin-created) buyers. Never compares across tenants.
            var query = _context.PublicCustomers
                .Include(p => p.Country)
                .AsQueryable();

            if (!isAdmin)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany == null) return;
                query = query.Where(p => p.CreatedByCompanyId == userCompany.PartyInfoId);
            }
            else
            {
                query = query.Where(p => p.CreatedByCompanyId == null);
            }

            var buyers = await query.ToListAsync();

            var byTin = buyers
                .Where(b => !string.IsNullOrWhiteSpace(b.TIN))
                .GroupBy(b => b.TIN.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateGroup { Reason = "Matching TIN", MatchValue = g.Key, Buyers = g.ToList() });

            var tinDuplicateIds = byTin.SelectMany(g => g.Buyers.Select(b => b.PublicCustomerId)).ToHashSet();

            var byName = buyers
                .Where(b => !string.IsNullOrWhiteSpace(b.CompanyName))
                .GroupBy(b => b.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && !g.All(b => tinDuplicateIds.Contains(b.PublicCustomerId)))
                .Select(g => new DuplicateGroup { Reason = "Matching Company Name", MatchValue = g.Key, Buyers = g.ToList() });

            DuplicateGroups = byTin.Concat(byName).ToList();
        }
    }
}
