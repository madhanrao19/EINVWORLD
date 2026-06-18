using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Services
{
    public class BuyerService : IBuyerService
    {
        private readonly ApplicationDbContext _context;

        public BuyerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddBuyersAsync(List<Buyer> buyers)
        {
            await _context.Buyers.AddRangeAsync(buyers);
            await _context.SaveChangesAsync();
        }

        public async Task<List<SelectListItem>> GetCombinedBuyersBySupplierAsync(int supplierId)
        {
            // 1. Fetch all assignments for this supplier, including the related buyer entities
            var supplierBuyers = await _context.SupplierBuyers
                .Where(sb => sb.SupplierId == supplierId)
                .Include(sb => sb.Buyer)           // Joins PartyInfo
                .Include(sb => sb.PublicCustomer)  // Joins PublicCustomer
                .AsNoTracking()
                .ToListAsync();

            var combinedList = new List<SelectListItem>();
            var addedPartyInfoIds = new HashSet<int>();

            foreach (var item in supplierBuyers)
            {
                // 2. Handle Standard Registered Buyers (PartyInfo)
                // Use Prefix "PI_"
                if (item.BuyerId.HasValue && item.Buyer != null)
                {
                    combinedList.Add(new SelectListItem
                    {
                        Text = item.Buyer.CompanyName,
                        Value = $"PI_{item.BuyerId}"
                    });
                    addedPartyInfoIds.Add(item.BuyerId.Value);
                }
                // 3. Handle Public Customers
                // Use Prefix "PC_"
                else if (item.PublicCustomerId.HasValue && item.PublicCustomer != null)
                {
                    combinedList.Add(new SelectListItem
                    {
                        Text = $"{item.PublicCustomer.CompanyName} (Buyer)", // Added suffix for clarity
                        Value = $"PC_{item.PublicCustomerId}"
                    });
                }
            }

            // 4. Always include PartyInfoId = 2, 3, 5, and the company itself (supplierId)
            var mandatoryIds = new List<int> { 2, 3, 5, supplierId };

            // Find which mandatory IDs are not already in the list
            var idsToFetch = mandatoryIds.Where(id => !addedPartyInfoIds.Contains(id)).Distinct().ToList();

            if (idsToFetch.Any())
            {
                var mandatoryParties = await _context.PartyInfos
                    .Where(p => idsToFetch.Contains(p.PartyInfoId))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var party in mandatoryParties)
                {
                    combinedList.Add(new SelectListItem
                    {
                        Text = party.CompanyName,
                        Value = $"PI_{party.PartyInfoId}"
                    });
                }
            }

            // 5. Return the merged list sorted by Company Name
            return combinedList.OrderBy(x => x.Text).ToList();
        }
    }
}