using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin")]
    public class AssignBuyersModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AssignBuyersModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public PartyInfo Supplier { get; set; } = null!;
        public List<PartyInfo> Buyers { get; set; } = new();
        public List<PartyInfo> AssignedBuyers { get; set; } = new();
        public List<int> SelectedBuyerIds { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int supplierId)
        {
            Supplier = await _context.PartyInfos
                                      .FirstOrDefaultAsync(s => s.PartyInfoId == supplierId) ?? null!;

            if (Supplier == null)
            {
                return NotFound();
            }

            // Get all buyers (not assigned to this supplier yet)
            Buyers = await _context.PartyInfos
                                   .Where(b => !_context.SupplierBuyers
                                       .Any(sb => sb.SupplierId == supplierId && sb.BuyerId == b.PartyInfoId))
                                   .ToListAsync();

            // Get all assigned buyers for the supplier
            AssignedBuyers = (await _context.SupplierBuyers
                .Where(sb => sb.SupplierId == supplierId && sb.BuyerId != null)
                .Select(sb => sb.Buyer)
                .ToListAsync())
                .Where(x => x != null).Cast<PartyInfo>().ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int supplierId)
        {
            // Get the supplier again
            Supplier = await _context.PartyInfos
                                      .FirstOrDefaultAsync(s => s.PartyInfoId == supplierId) ?? null!;

            if (Supplier == null)
            {
                return NotFound();
            }

            // Process the selected buyers
            if (SelectedBuyerIds != null)
            {
                foreach (var buyerId in SelectedBuyerIds)
                {
                    var existingRecord = await _context.SupplierBuyers
                                                         .FirstOrDefaultAsync(sb => sb.SupplierId == supplierId && sb.BuyerId == buyerId);
                    if (existingRecord == null)
                    {
                        // Add buyer to supplier relationship
                        _context.SupplierBuyers.Add(new SupplierBuyer
                        {
                            SupplierId = supplierId,
                            BuyerId = buyerId
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { supplierId });
        }

        public async Task<IActionResult> OnPostUnassignAsync(int supplierId, List<int> UnassignedBuyerIds)
        {
            if (UnassignedBuyerIds != null)
            {
                var supplierBuyersToRemove = _context.SupplierBuyers
                    .Where(sb => sb.SupplierId == supplierId
                                 && sb.BuyerId != null
                                 && UnassignedBuyerIds.Contains(sb.BuyerId.Value))
                    .ToList();

                _context.SupplierBuyers.RemoveRange(supplierBuyersToRemove);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { supplierId });
        }
    }

}
