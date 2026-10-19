using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Who may see a party's bank details on the invoice screens (Create/Edit "LoadPartyDetails"):
    /// an Admin; a member of the company itself ("PI"); or, for a public customer ("PC"), a member of the
    /// company that created it or is linked to it. Everyone else gets the party without bank details.
    /// </summary>
    public static class PartyBankAccess
    {
        public static async Task<bool> CanSeeBankAsync(ClaimsPrincipal user, ApplicationDbContext db, string? partyType, int partyId)
        {
            if (user.IsInRole("Admin")) return true;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return false;

            var companyIds = await db.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfoId)
                .ToListAsync();
            if (companyIds.Count == 0) return false;

            return partyType switch
            {
                "PI" => companyIds.Contains(partyId),
                "PC" => await db.PublicCustomers.AnyAsync(p => p.PublicCustomerId == partyId
                            && p.CreatedByCompanyId.HasValue && companyIds.Contains(p.CreatedByCompanyId.Value))
                        || await db.SupplierBuyers.AnyAsync(sb => sb.PublicCustomerId == partyId && companyIds.Contains(sb.SupplierId)),
                _ => false,
            };
        }
    }
}
