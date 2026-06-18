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
        public HashSet<string> AssignedBuyerKeys { get; set; } = new();
        public HashSet<int> AssignedPublicCustomerIds { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

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

            var query = from pc in _context.PublicCustomers
                            .Include(p => p.State)
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
                    TempData["ErrorMessage"] = $"DEBUG FAIL: userCompany is NULL. Your UserID is {userId}.";
                    return RedirectToPage();
                }
                if (entity.CreatedByCompanyId != userCompany.PartyInfoId)
                {
                    TempData["ErrorMessage"] = $"DEBUG FAIL: ID Mismatch! Buyer was created by Company ID '{entity.CreatedByCompanyId}', but your active Company ID is '{userCompany.PartyInfoId}'.";
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