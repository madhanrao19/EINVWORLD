using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Services;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Server-side check for the ids an invoice form posts (Create e-Invoice / Invoice Edit): never trust
    /// them. Returns null when allowed, otherwise the message to show. Mirrors what the pages offer:
    /// FilterSuppliers (own companies; general TINs for self-billed) and LoadCustomers (the supplier's
    /// buyer list; own companies for self-billed).
    /// </summary>
    public static class InvoiceWriteGuard
    {
        public static readonly string[] SelfBilledTypes = { "11", "12", "13", "14" };
        private static readonly string[] SelfBilledSupplierTins = { "EI00000000010", "EI00000000030" };
        private static readonly string[] EditableStatuses = { "Draft", "TransmissionError" };

        public static async Task<string?> CheckAsync(ClaimsPrincipal user, ApplicationDbContext db, IBuyerService buyers,
            string? invoiceNo, string? docTypeCode, int? supplierId, int? customerId, int? publicCustomerId, bool checkInvoiceNo = true)
        {
            if (checkInvoiceNo && !string.IsNullOrWhiteSpace(invoiceNo))
            {
                // The number is also the draft file name (Drafts\{InvoiceNo}.json).
                if (invoiceNo != Path.GetFileName(invoiceNo) || invoiceNo.Contains("..") || invoiceNo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return "Invalid invoice number.";

                // Save Draft overwrites the row with this number, so an existing one must be the caller's
                // own invoice, still in an editable status.
                var existing = await db.InvoiceHeaders.Where(h => h.InvoiceNo == invoiceNo).Select(h => new { h.InternalStatusId }).FirstOrDefaultAsync();
                if (existing != null)
                {
                    if (!await user.CanManageInvoiceAsync(db, invoiceNo)) return "You are not authorized to edit this invoice.";
                    if (!EditableStatuses.Contains(existing.InternalStatusId)) return "This invoice can no longer be edited.";
                }
            }

            if (user.IsAdmin(db)) return null;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            // View-only memberships can't issue or receive (self-billed) invoices: view, print and download only.
            var ownCompanyIds = await db.UserCompanies.Where(uc => uc.UserId == userId && !uc.IsViewOnly).Select(uc => uc.PartyInfoId).ToListAsync();
            bool selfBilled = SelfBilledTypes.Contains(docTypeCode);

            if (supplierId is int sid)
            {
                bool supplierOk = selfBilled
                    ? await db.PartyInfos.AnyAsync(p => p.PartyInfoId == sid && SelfBilledSupplierTins.Contains(p.TIN))
                    : ownCompanyIds.Contains(sid);
                if (!supplierOk) return "You can't issue invoices as this supplier.";
            }

            if (selfBilled)
            {
                // Self-billed: the user's own company is the buyer (and the issuer).
                if (publicCustomerId > 0 || (customerId is int cid && !ownCompanyIds.Contains(cid)))
                    return "For self-billed documents the buyer must be one of your companies.";
            }
            else if (supplierId is int s && (customerId > 0 || publicCustomerId > 0))
            {
                var value = customerId > 0 ? $"PI_{customerId}" : $"PC_{publicCustomerId}";
                var allowed = await buyers.GetCombinedBuyersBySupplierAsync(s);
                if (!allowed.Any(b => b.Value == value)) return "This buyer isn't linked to the selected supplier.";
            }

            return null;
        }
    }
}
