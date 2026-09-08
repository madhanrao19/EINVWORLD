using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.PublicCustomer
{
    [Authorize(Roles = "Admin,Supplier")]
    public class ListModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;

        public ListModel(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public class PublicCustomerViewModel
        {
            public eInvWorld.Models.InputModel.PublicCustomer Customer { get; set; } = default!;
            public string CreatorCompanyName { get; set; } = string.Empty;
        }

        public IList<PublicCustomerViewModel> CustomerViewModels { get; set; } = default!;
        public Dictionary<string, string> StateNames { get; set; } = new();
        public HashSet<string> AssignedBuyerKeys { get; set; } = new();
        public HashSet<int> AssignedPublicCustomerIds { get; set; } = new();
        public Dictionary<int, int> InvoiceCountByBuyer { get; set; } = new();

        // Real KPI counts, scoped the same way as the main query, before search filtering.
        public int TotalBuyersCount { get; set; }
        public int ActiveBuyersCount { get; set; }
        public int AddedThisMonthCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        // "active" | "inactive" | null/"" = all. Purely additive filter on the existing IsActive flag.
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
            bool isAdmin = User.IsInRole("Admin");

            StateNames = await _context.StateCodes.ToDictionaryAsync(s => s.Code, s => s.State);

            var query = from pc in _context.PublicCustomers
                            .Include(p => p.Country)
                        join party in _context.PartyInfos
                            on pc.CreatedByCompanyId equals party.PartyInfoId into pcParty
                        from party in pcParty.DefaultIfEmpty()
                        select new PublicCustomerViewModel
                        {
                            Customer = pc,
                            CreatorCompanyName = party != null ? party.CompanyName : "-"
                        };

            // 1. APPLY SEARCH
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(q =>
                    q.Customer.CompanyName.Contains(SearchTerm) ||
                    q.Customer.TIN.Contains(SearchTerm) ||
                    (q.Customer.Email != null && q.Customer.Email.Contains(SearchTerm)) ||
                    q.CreatorCompanyName.Contains(SearchTerm));
            }

            if (string.Equals(StatusFilter, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(q => q.Customer.IsActive);
            }
            else if (string.Equals(StatusFilter, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(q => !q.Customer.IsActive);
            }

            // 2. APPLY ROLE FILTERS
            if (isAdmin)
            {
                var assignments = await _context.SupplierBuyers
                    .Where(sb => sb.PublicCustomerId != null)
                    .Select(sb => new { sb.SupplierId, sb.PublicCustomerId })
                    .ToListAsync();

                AssignedBuyerKeys = assignments.Select(x => $"{x.SupplierId}_{x.PublicCustomerId}").ToHashSet();
                AssignedPublicCustomerIds = assignments.Where(x => x.PublicCustomerId.HasValue)
                                                       .Select(x => x.PublicCustomerId!.Value)
                                                       .ToHashSet();
            }
            else
            {
                var userCompany = await _context.UserCompanies
                     .Where(uc => uc.UserId == userId)
                     .OrderByDescending(uc => uc.IsPrimaryCompany)
                     .FirstOrDefaultAsync();

                if (userCompany != null)
                {
                    query = query.Where(p => p.Customer.CreatedByCompanyId == userCompany.PartyInfoId);

                    var assignedData = await _context.SupplierBuyers
                        .Where(sb => sb.SupplierId == userCompany.PartyInfoId && sb.PublicCustomerId != null)
                        .Select(sb => sb.PublicCustomerId)
                        .ToListAsync();

                    foreach (var pcId in assignedData)
                    {
                        AssignedBuyerKeys.Add($"{userCompany.PartyInfoId}_{pcId}");
                    }
                }
                else
                {
                    query = query.Where(p => false);
                }
            }

            // 2b. REAL KPI COUNTS — scoped by role like the table, but computed before the search
            // filter so the tiles reflect the whole directory regardless of what's typed in the box.
            var kpiScope = _context.PublicCustomers.AsQueryable();
            if (!isAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();
                kpiScope = userCompany != null
                    ? kpiScope.Where(p => p.CreatedByCompanyId == userCompany.PartyInfoId)
                    : kpiScope.Where(p => false);
            }
            TotalBuyersCount = await kpiScope.CountAsync();
            ActiveBuyersCount = await kpiScope.CountAsync(p => p.IsActive);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            AddedThisMonthCount = await kpiScope.CountAsync(p => p.CreatedDate >= monthStart);

            var buyerIds = await kpiScope.Select(p => p.PublicCustomerId).ToListAsync();
            InvoiceCountByBuyer = await _context.InvoiceHeaders
                .Where(i => i.PublicCustomerId.HasValue && buyerIds.Contains(i.PublicCustomerId.Value))
                .GroupBy(i => i.PublicCustomerId!.Value)
                .Select(g => new { BuyerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.BuyerId, g => g.Count);

            // 3. APPLY SORTING
            bool isDesc = SortOrder == "desc";

            switch (SortBy?.ToLower())
            {
                case "name":
                    query = isDesc ? query.OrderByDescending(q => q.Customer.CompanyName) : query.OrderBy(q => q.Customer.CompanyName);
                    break;
                case "tin":
                    query = isDesc ? query.OrderByDescending(q => q.Customer.TIN) : query.OrderBy(q => q.Customer.TIN);
                    break;
                case "email":
                    query = isDesc ? query.OrderByDescending(q => q.Customer.Email) : query.OrderBy(q => q.Customer.Email);
                    break;
                case "phone":
                    query = isDesc ? query.OrderByDescending(q => q.Customer.PhoneNo) : query.OrderBy(q => q.Customer.PhoneNo);
                    break;
                case "updated":
                    query = isDesc ? query.OrderByDescending(q => q.Customer.UpdatedDate) : query.OrderBy(q => q.Customer.UpdatedDate);
                    break;
                default:
                    query = query.OrderByDescending(q => q.Customer.CreatedDate);
                    break;
            }

            // ✅ 4. APPLY PAGINATION
            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            CustomerViewModels = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }


        // Exports the same filtered set the on-screen table shows (search + status + role/company
        // scoping), but without pagination. Scoping mirrors OnGetAsync exactly — an Admin sees every
        // buyer, a Supplier only their own company's (CreatedByCompanyId == userCompany.PartyInfoId) —
        // so this can never leak another company's buyers the way the pre-v1.9.8 invoice export did.
        public async Task<IActionResult> OnGetExportCsvAsync(string? searchTerm, string? statusFilter)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isAdmin = User.IsInRole("Admin");

            var query = from pc in _context.PublicCustomers.Include(p => p.Country)
                        join party in _context.PartyInfos
                            on pc.CreatedByCompanyId equals party.PartyInfoId into pcParty
                        from party in pcParty.DefaultIfEmpty()
                        select new PublicCustomerViewModel
                        {
                            Customer = pc,
                            CreatorCompanyName = party != null ? party.CompanyName : "-"
                        };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(q =>
                    q.Customer.CompanyName.Contains(searchTerm) ||
                    q.Customer.TIN.Contains(searchTerm) ||
                    (q.Customer.Email != null && q.Customer.Email.Contains(searchTerm)) ||
                    q.CreatorCompanyName.Contains(searchTerm));
            }

            if (string.Equals(statusFilter, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(q => q.Customer.IsActive);
            }
            else if (string.Equals(statusFilter, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(q => !q.Customer.IsActive);
            }

            if (!isAdmin)
            {
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                query = userCompany != null
                    ? query.Where(p => p.Customer.CreatedByCompanyId == userCompany.PartyInfoId)
                    : query.Where(p => false);
            }

            var buyers = await query.OrderBy(q => q.Customer.CompanyName).ToListAsync();

            var stateNames = await _context.StateCodes.ToDictionaryAsync(s => s.Code, s => s.State);
            var regTypeNames = await _context.RegistrationTypes.ToDictionaryAsync(r => r.Code, r => r.Name);
            var msicDescriptions = await _context.MSICSubCategoryCodes.ToDictionaryAsync(m => m.Code, m => m.Description);

            var headers = new[]
            {
                "Company Name", "TIN", "Registration Type", "Registration No", "Old Registration No",
                "SST No", "Tourism Tax No", "MSIC Code", "Business Description",
                "Primary Email", "Phone", "Fax", "Address", "Postal Code", "City", "State", "Country",
                "Bank Account No", "Bank Name", "Default Payment Terms", "Attention To",
                "Authorisation Number", "Remarks", "Status", "Creator Company"
            };
            var rows = buyers.Select(b => new[]
            {
                b.Customer.CompanyName,
                b.Customer.TIN,
                regTypeNames.GetValueOrDefault(b.Customer.RegTypeCode, b.Customer.RegTypeCode),
                b.Customer.RegNo,
                b.Customer.OldRegNo,
                b.Customer.SST,
                b.Customer.TTX,
                msicDescriptions.TryGetValue(b.Customer.IndustryClassificationCode, out var msicDesc)
                    ? $"{b.Customer.IndustryClassificationCode} - {msicDesc}"
                    : b.Customer.IndustryClassificationCode,
                b.Customer.BizDescription,
                b.Customer.Email,
                b.Customer.PhoneNo,
                b.Customer.FaxNo,
                string.Join(" ", new[] { b.Customer.Addr1, b.Customer.Addr2, b.Customer.Addr3 }.Where(a => !string.IsNullOrWhiteSpace(a))),
                b.Customer.PostalCode,
                b.Customer.CityName,
                stateNames.GetValueOrDefault(b.Customer.StateCode, b.Customer.StateCode),
                b.Customer.Country?.Country ?? b.Customer.CountryCode,
                b.Customer.BankAccountNo,
                b.Customer.BankName,
                b.Customer.PaymentTerms,
                b.Customer.Attention,
                b.Customer.AuthorisationNumber,
                b.Customer.Remarks,
                b.Customer.IsActive ? "Active" : "Inactive",
                b.CreatorCompanyName
            });

            var csvBytes = CsvExportHelper.BuildCsv(headers, rows);
            var timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur")).ToString("ddMMyyyy_HHmmss");
            return File(csvBytes, "text/csv", $"BuyerDirectory_{timestamp}.csv");
        }

        public async Task<IActionResult> OnPostDeleteAsync(int buyerId)
        {
            var entity = await _context.PublicCustomers.FindAsync(buyerId);
            if (entity == null) return NotFound();

            bool hasLinkedInvoices = await _context.InvoiceHeaders.AnyAsync(inv => inv.PublicCustomerId == buyerId);
            if (hasLinkedInvoices)
            {
                TempData["ErrorMessage"] = "Cannot delete this buyer because there are invoices linked to it.";
                return RedirectToPage();
            }

            bool hasLinkedTemplates = await _context.InvoiceTemplates.AnyAsync(t => t.PublicCustomerId == buyerId);
            if (hasLinkedTemplates)
            {
                TempData["ErrorMessage"] = "Cannot delete this buyer because there are invoice templates linked to it.";
                return RedirectToPage();
            }

            int? supplierIdToCheck = null;

            if (User.IsInRole("Admin"))
            {
                supplierIdToCheck = entity.CreatedByCompanyId;
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany == null)
                {
                    TempData["ErrorMessage"] = "Your account is not linked to a company.";
                    return RedirectToPage();
                }
                if (entity.CreatedByCompanyId != userCompany.PartyInfoId)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage this buyer.";
                    return RedirectToPage();
                }

                supplierIdToCheck = userCompany.PartyInfoId;
            }

            if (supplierIdToCheck.HasValue)
            {
                var existingAssignment = await _context.SupplierBuyers
                    .FirstOrDefaultAsync(sb => sb.SupplierId == supplierIdToCheck.Value && sb.PublicCustomerId == buyerId);

                if (existingAssignment != null)
                {
                    _context.SupplierBuyers.Remove(existingAssignment);
                }
            }

            // Other suppliers may still be assigned to this buyer (e.g. an Admin-shared buyer, or one
            // linked to multiple suppliers by an Admin). Only hard-delete the shared PublicCustomer row
            // once no other SupplierBuyer link references it — otherwise this action is just an unlink.
            bool hasOtherAssignments = await _context.SupplierBuyers
                .AnyAsync(sb => sb.PublicCustomerId == buyerId
                    && (!supplierIdToCheck.HasValue || sb.SupplierId != supplierIdToCheck.Value));

            if (hasOtherAssignments)
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Buyer removed from your list.";
                return RedirectToPage();
            }

            _context.PublicCustomers.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Buyer deleted and unassigned successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddToBuyersAsync(int publicCustomerId)
        {
            var publicCustomer = await _context.PublicCustomers.FindAsync(publicCustomerId);
            if (publicCustomer == null) return NotFound();

            int? supplierIdToLink = null;
            if (User.IsInRole("Admin"))
            {
                supplierIdToLink = publicCustomer.CreatedByCompanyId;
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany != null) supplierIdToLink = userCompany.PartyInfoId;
            }

            if (supplierIdToLink == null)
            {
                TempData["ErrorMessage"] = "Could not identify a supplier account.";
                return RedirectToPage();
            }

            var linkExists = await _context.SupplierBuyers
                .AnyAsync(sb => sb.SupplierId == supplierIdToLink && sb.PublicCustomerId == publicCustomerId);

            if (!linkExists)
            {
                _context.SupplierBuyers.Add(new SupplierBuyer
                {
                    SupplierId = supplierIdToLink.Value,
                    BuyerId = null,
                    PublicCustomerId = publicCustomerId
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Buyer successfully added to your list (Direct Link).";
            }
            else
            {
                TempData["ErrorMessage"] = "This buyer is already in your list.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveFromBuyersAsync(int publicCustomerId)
        {
            var publicCustomer = await _context.PublicCustomers.FindAsync(publicCustomerId);
            if (publicCustomer == null) return NotFound();

            int? supplierIdToUnlink = null;
            if (User.IsInRole("Admin"))
            {
                supplierIdToUnlink = publicCustomer.CreatedByCompanyId;
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userCompany = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .OrderByDescending(uc => uc.IsPrimaryCompany)
                    .FirstOrDefaultAsync();

                if (userCompany != null) supplierIdToUnlink = userCompany.PartyInfoId;
            }

            if (supplierIdToUnlink == null) return RedirectToPage();

            var link = await _context.SupplierBuyers
                .FirstOrDefaultAsync(sb => sb.SupplierId == supplierIdToUnlink && sb.PublicCustomerId == publicCustomerId);

            if (link != null)
            {
                _context.SupplierBuyers.Remove(link);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Buyer removed from your list.";
            }

            return RedirectToPage();
        }
    }
}