using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;

namespace EINVWORLD.Helpers
{
    public static class UserExtensions
    {
        /// <summary>
        /// ✅ Returns the first TIN (Taxpayer Identification Number) associated with the user.
        /// Use this when you expect only one company per user.
        /// </summary>
        public static string? GetUserTIN(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return dbContext.UserCompanies
                .Include(uc => uc.PartyInfo)
                .Where(uc => uc.User.UserName == userId)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefault(); // ⛔ Returns only the first if multiple exist
        }

        /// <summary>
        /// ✅ Returns the first PartyInfoId (company ID) associated with the user.
        /// Use this only if the user is guaranteed to be linked to a single company.
        /// </summary>
        public static int? GetUserPartyInfoId(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return dbContext.UserCompanies
                .Include(uc => uc.PartyInfo)
                .Where(uc => uc.User.UserName == userId)
                .Select(uc => uc.PartyInfo.PartyInfoId)
                .FirstOrDefault(); // ⛔ Only returns one ID
        }

        /// <summary>
        /// ✅ Returns true if the user is marked as a Buyer (based on UserType).
        /// Used for role-based dashboard or page access.
        /// </summary>
        public static bool IsBuyer(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return false;

            return dbContext.Users
                .Where(u => u.UserName == userId)
                .Select(u => u.UserType)
                .FirstOrDefault() == "Buyer";
        }

        /// <summary>
        /// ✅ Returns all PartyInfoIds (company IDs) linked to the user.
        /// Use this for filtering invoices, dashboards, or data across multiple companies.
        /// </summary>
        public static List<int> GetUserCompanyIds(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return new List<int>();

            return dbContext.UserCompanies
                .Where(uc => uc.User.UserName == userId)
                .Select(uc => uc.PartyInfoId)
                .ToList();
        }

        /// <summary>
        /// ✅ Returns all PartyInfo objects (full company details) linked to the user.
        /// Useful when displaying company dropdowns or performing full audits.
        /// </summary>
        public static List<PartyInfo> GetUserCompanies(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return new List<PartyInfo>();

            return dbContext.UserCompanies
                .Include(uc => uc.PartyInfo)
                .Where(uc => uc.User.UserName == userId)
                .Select(uc => uc.PartyInfo)
                .ToList();
        }

        public static bool IsAdmin(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return false;

            return dbContext.Users
                .Where(u => u.UserName == userId)
                .Select(u => u.UserType)
                .FirstOrDefault() == "Admin";
        }

        public static bool IsFinance(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId)) return false;

            return dbContext.Users
                .Where(u => u.UserName == userId)
                .Select(u => u.UserType)
                .FirstOrDefault() == "Finance";
        }

        /// <summary>
        /// Authorization check for invoice access (prevents IDOR on download/detail endpoints).
        /// A user may access an invoice only if one of its party TINs (Supplier / Customer /
        /// PublicCustomer) belongs to one of the user's companies — the SAME rule the invoice list
        /// uses for visibility — or if the user is an Admin.
        /// </summary>
        public static async Task<bool> CanAccessInvoiceAsync(this ClaimsPrincipal user, ApplicationDbContext db, string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return false;
            if (user.IsAdmin(db)) return true;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var userTins = await db.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfo.TIN)
                .ToListAsync();
            // LHDN general TINs (EI00000000010 General Public, ...) are shared by every tenant's invoices;
            // a membership of such a party must never grant access to all of them.
            userTins.RemoveAll(GeneralTINHelper.IsGeneralTIN);
            if (userTins.Count == 0) return false;

            var party = await db.InvoiceHeaders
                .Where(i => i.InvoiceNo == invoiceNo)
                .Select(i => new
                {
                    S = (string?)i.Supplier.TIN,
                    C = i.Customer != null ? i.Customer.TIN : null,
                    P = i.PublicCustomer != null ? i.PublicCustomer.TIN : null
                })
                .FirstOrDefaultAsync();
            if (party == null) return false;

            return (party.S != null && userTins.Contains(party.S))
                || (party.C != null && userTins.Contains(party.C))
                || (party.P != null && userTins.Contains(party.P));
        }

        /// <summary>Same ownership check as <see cref="CanAccessInvoiceAsync"/> but keyed by the document UUID (for cancel/reject handlers).</summary>
        public static async Task<bool> CanAccessInvoiceByUuidAsync(this ClaimsPrincipal user, ApplicationDbContext db, string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid)) return false;
            if (user.IsAdmin(db)) return true;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var userTins = await db.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfo.TIN)
                .ToListAsync();
            // LHDN general TINs (EI00000000010 General Public, ...) are shared by every tenant's invoices;
            // a membership of such a party must never grant access to all of them.
            userTins.RemoveAll(GeneralTINHelper.IsGeneralTIN);
            if (userTins.Count == 0) return false;

            var party = await db.InvoiceHeaders
                .Where(i => i.UUID == uuid)
                .Select(i => new
                {
                    S = (string?)i.Supplier.TIN,
                    C = i.Customer != null ? i.Customer.TIN : null,
                    P = i.PublicCustomer != null ? i.PublicCustomer.TIN : null
                })
                .FirstOrDefaultAsync();
            if (party == null) return false;

            return (party.S != null && userTins.Contains(party.S))
                || (party.C != null && userTins.Contains(party.C))
                || (party.P != null && userTins.Contains(party.P));
        }

        /// <summary>
        /// Returns true if the user may submit/act AS <paramref name="tin"/>: admins always may;
        /// otherwise the TIN must be one of the user's assigned company TINs (UserCompanies).
        /// CanAccessInvoiceAsync only checks read access (owns supplier OR customer TIN), so this is the
        /// stronger gate needed before submitting under a specific issuer TIN — it stops a counterparty
        /// who only owns the other party's TIN from submitting as the invoice issuer.
        /// </summary>
        public static async Task<bool> OwnsTinAsync(this ClaimsPrincipal user, ApplicationDbContext db, string? tin)
        {
            if (string.IsNullOrWhiteSpace(tin)) return false;
            if (user.IsAdmin(db)) return true;
            if (GeneralTINHelper.IsGeneralTIN(tin)) return false; // shared by all tenants; never "owned"

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return false;

            return await db.UserCompanies
                .Where(uc => uc.UserId == userId)
                .AnyAsync(uc => uc.PartyInfo.TIN == tin);
        }

        /// <summary>
        /// Write access to an invoice (edit / delete): the user must own the ISSUER's TIN — the supplier,
        /// or the customer for self-billed types 11–14 (the same rule submission uses). Unlike
        /// <see cref="CanAccessInvoiceAsync"/>, the counterparty that merely receives the invoice is refused.
        /// </summary>
        public static async Task<bool> CanManageInvoiceAsync(this ClaimsPrincipal user, ApplicationDbContext db, string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return false;
            if (user.IsAdmin(db)) return true;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var issuer = await db.InvoiceHeaders
                .Where(i => i.InvoiceNo == invoiceNo)
                .Select(i => i.DocTypeCode == "11" || i.DocTypeCode == "12" || i.DocTypeCode == "13" || i.DocTypeCode == "14"
                    ? new { Id = i.CustomerId, Tin = i.Customer != null ? i.Customer.TIN : null }
                    : new { Id = (int?)i.SupplierId, Tin = (string?)i.Supplier.TIN })
                .FirstOrDefaultAsync();

            // Match the issuing company itself (not its TIN), and never treat a shared LHDN general TIN
            // (EI00000000010 etc.) as owned: those parties are common to every tenant, so membership of one
            // must not grant write access to every invoice issued under it.
            if (issuer?.Id is not int issuerId || GeneralTINHelper.IsGeneralTIN(issuer.Tin)) return false;

            return await db.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == issuerId);
        }

    }
}
