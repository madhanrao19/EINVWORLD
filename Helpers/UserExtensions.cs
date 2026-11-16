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

            // A shared LHDN general-TIN party (General Public, Foreign ...) is on every tenant's invoices, so it never
            // scopes invoices to this user; the Invoice List drops general TINs the same way.
            return dbContext.UserCompanies
                .Where(uc => uc.User.UserName == userId)
                .Select(uc => new { uc.PartyInfoId, uc.PartyInfo.TIN })
                .AsEnumerable()
                .Where(uc => !GeneralTINHelper.IsGeneralTIN(uc.TIN))
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

        /// <summary>
        /// Admin for invoice/company data checks. Page gates use the Identity role, so both the role and
        /// UserType must say Admin: if they ever drift apart, the user gets less access, never more.
        /// </summary>
        public static bool IsAdmin(this ClaimsPrincipal user, ApplicationDbContext dbContext)
        {
            var userId = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userId) || !user!.IsInRole("Admin")) return false;

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
        /// A user may access an invoice only if <see cref="VisibleToTins"/> allows it for the user's company TINs
        /// (the issuer always; the recipient once it reached LHDN — the SAME rule the invoice list uses), or if
        /// the user is an Admin.
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

            return await db.InvoiceHeaders.Where(i => i.InvoiceNo == invoiceNo).Where(VisibleToTins(userTins)).AnyAsync();
        }

        /// <summary>
        /// The invoices a non-Admin holding <paramref name="tins"/> may see: everything their company issues, in any
        /// status, and what they receive once LHDN has validated it — <see cref="ReceivedByTins"/>: LHDN status Valid
        /// or Cancelled (LHDN cancels only validated documents; a buyer's rejection leaves it Valid). Never Draft,
        /// pending (Submitted) or Invalid. Self-billed types 11–14 (incl. CN/DN/RN 12–14) are issued by the customer.
        /// Translates to SQL; callers remove general TINs from <paramref name="tins"/> first.
        /// </summary>
        public static System.Linq.Expressions.Expression<Func<InvoiceHeader, bool>> VisibleToTins(List<string> tins)
        {
            // IssuedByTins OR ReceivedByTins, as one SQL-translatable predicate (the two rules live in one place each).
            var issued = IssuedByTins(tins);
            var received = ReceivedByTins(tins);
            var i = issued.Parameters[0];
            var body = System.Linq.Expressions.Expression.OrElse(issued.Body, new Rebind(received.Parameters[0], i).Visit(received.Body));
            return System.Linq.Expressions.Expression.Lambda<Func<InvoiceHeader, bool>>(body, i);
        }

        private sealed class Rebind(System.Linq.Expressions.ParameterExpression from, System.Linq.Expressions.ParameterExpression to)
            : System.Linq.Expressions.ExpressionVisitor
        {
            protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node) =>
                node == from ? to : base.VisitParameter(node);
        }

        /// <summary>The recipient side of <see cref="VisibleToTins"/>: documents addressed to a company holding
        /// <paramref name="tins"/> (self-billed 11–14: the supplier receives) that LHDN has validated — a UUID and one of
        /// <see cref="DeliveredLhdnStatuses"/>. The Received tab and the Buyer dashboard use exactly this.</summary>
        public static System.Linq.Expressions.Expression<Func<InvoiceHeader, bool>> ReceivedByTins(List<string> tins)
        {
            var selfBilled = new[] { "11", "12", "13", "14" };
            return i => i.UUID != null && i.UUID != "" && DeliveredLhdnStatuses.Contains(i.LHDNStatusId!)
                && ((!selfBilled.Contains(i.DocTypeCode) && ((i.Customer != null && tins.Contains(i.Customer.TIN))
                                                             || (i.PublicCustomer != null && tins.Contains(i.PublicCustomer.TIN))))
                    || (selfBilled.Contains(i.DocTypeCode) && tins.Contains(i.Supplier.TIN)));
        }

        /// <summary>The issuer side of <see cref="VisibleToTins"/>: invoices a company holding <paramref name="tins"/> issued
        /// (self-billed 11–14: the customer). For data only the issuer may see, e.g. its drafts or its staff's usernames.</summary>
        public static System.Linq.Expressions.Expression<Func<InvoiceHeader, bool>> IssuedByTins(List<string> tins)
        {
            var selfBilled = new[] { "11", "12", "13", "14" };
            return i =>
                (!selfBilled.Contains(i.DocTypeCode) && tins.Contains(i.Supplier.TIN)) ||
                (selfBilled.Contains(i.DocTypeCode) && ((i.Customer != null && tins.Contains(i.Customer.TIN))
                                                       || (i.PublicCustomer != null && tins.Contains(i.PublicCustomer.TIN))));
        }

        /// <summary>Company TINs for <paramref name="companyIds"/>, without the shared LHDN general TINs (for the rules above).</summary>
        public static List<string> TinsOf(ApplicationDbContext db, IEnumerable<int> companyIds)
        {
            var ids = companyIds.ToList();
            return db.PartyInfos.AsNoTracking().Where(p => ids.Contains(p.PartyInfoId) && p.TIN != null)
                .Select(p => p.TIN!).ToList().Where(t => !GeneralTINHelper.IsGeneralTIN(t)).Distinct().ToList();
        }

        /// <summary>LHDN statuses a recipient may see: validated, or cancelled/rejected after validation. Never
        /// Submitted (pending), Invalid or an unmapped value. "Rejected" is defensive: LHDN leaves a rejected document Valid.</summary>
        public static readonly string[] DeliveredLhdnStatuses = { "Valid", "Cancelled", "Rejected" };

        /// <summary>Same ownership check as <see cref="CanAccessInvoiceAsync"/> but keyed by the document UUID (for cancel/reject handlers).
        /// <paramref name="forWrite"/> ignores view-only memberships: view-only members may only view, print and download.</summary>
        public static async Task<bool> CanAccessInvoiceByUuidAsync(this ClaimsPrincipal user, ApplicationDbContext db, string uuid, bool forWrite = false)
        {
            if (string.IsNullOrWhiteSpace(uuid)) return false;
            if (user.IsAdmin(db)) return true;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var userTins = await db.UserCompanies
                .Where(uc => uc.UserId == userId && (!forWrite || !uc.IsViewOnly))
                .Select(uc => uc.PartyInfo.TIN)
                .ToListAsync();
            // LHDN general TINs (EI00000000010 General Public, ...) are shared by every tenant's invoices;
            // a membership of such a party must never grant access to all of them.
            userTins.RemoveAll(GeneralTINHelper.IsGeneralTIN);
            if (userTins.Count == 0) return false;

            return await db.InvoiceHeaders.Where(i => i.UUID == uuid).Where(VisibleToTins(userTins)).AnyAsync();
        }

        /// <summary>
        /// Returns true if the user may submit/act AS <paramref name="tin"/>: admins always may;
        /// otherwise the TIN must be one of the user's assigned company TINs (UserCompanies).
        /// CanAccessInvoiceAsync only checks read access (owns supplier OR customer TIN), so this is the
        /// stronger gate needed before submitting under a specific issuer TIN — it stops a counterparty
        /// who only owns the other party's TIN from submitting as the invoice issuer. A view-only membership
        /// never counts: view-only members may only view, print and download.
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
                .AnyAsync(uc => uc.PartyInfo.TIN == tin && !uc.IsViewOnly);
        }

        /// <summary>
        /// Write access to an invoice (edit / delete): the user must own the ISSUER's TIN — the supplier,
        /// or the customer for self-billed types 11–14 (the same rule submission uses). Unlike
        /// <see cref="CanAccessInvoiceAsync"/>, the counterparty that merely receives the invoice is refused, and so
        /// is a view-only member of the issuer (view-only members may only view, print and download).
        /// </summary>
        public static async Task<bool> CanManageInvoiceAsync(this ClaimsPrincipal user, ApplicationDbContext db, string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return false;
            if (user.IsAdmin(db)) return true;

            var ids = await user.OwnCompanyIdsAsync(db, forWrite: true);
            return ids.Count > 0 && await db.InvoiceHeaders.Where(i => i.InvoiceNo == invoiceNo).Where(IssuedByCompanies(ids)).AnyAsync();
        }

        /// <summary>
        /// True when the user may see the issuer's internal records for an invoice (staff usernames, the full activity
        /// history): an Admin, or any member — view-only included — of the issuing company. A recipient is not.
        /// </summary>
        public static async Task<bool> IsIssuerOfInvoiceAsync(this ClaimsPrincipal user, ApplicationDbContext db, string invoiceNo)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) return false;
            if (user.IsAdmin(db)) return true;
            var ids = await user.OwnCompanyIdsAsync(db, forWrite: false);
            return ids.Count > 0 && await db.InvoiceHeaders.Where(i => i.InvoiceNo == invoiceNo).Where(IssuedByCompanies(ids)).AnyAsync();
        }

        /// <summary>
        /// The issuer rule for actions and internal data: the invoice's issuing company RECORD (self-billed 11–14: the
        /// customer) is one of <paramref name="companyIds"/>. Stricter than <see cref="IssuedByTins"/>, which decides
        /// visibility by TIN: an address-book PublicCustomer that merely shares a company's TIN never makes that company
        /// the issuer. Translates to SQL.
        /// </summary>
        public static System.Linq.Expressions.Expression<Func<InvoiceHeader, bool>> IssuedByCompanies(List<int> companyIds)
        {
            var selfBilled = new[] { "11", "12", "13", "14" };
            return i => (!selfBilled.Contains(i.DocTypeCode) && i.SupplierId != null && companyIds.Contains(i.SupplierId.Value))
                     || (selfBilled.Contains(i.DocTypeCode) && i.CustomerId != null && companyIds.Contains(i.CustomerId.Value));
        }

        /// <summary>The user's company ids, never a shared LHDN general-TIN party (common to every tenant).
        /// <paramref name="forWrite"/> drops view-only memberships (view, print and download only).</summary>
        public static async Task<List<int>> OwnCompanyIdsAsync(this ClaimsPrincipal user, ApplicationDbContext db, bool forWrite)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return new List<int>();

            return (await db.UserCompanies
                    .Where(uc => uc.UserId == userId && (!forWrite || !uc.IsViewOnly))
                    .Select(uc => new { uc.PartyInfoId, uc.PartyInfo.TIN })
                    .ToListAsync())
                .Where(uc => !GeneralTINHelper.IsGeneralTIN(uc.TIN))
                .Select(uc => uc.PartyInfoId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// The invoice number plus the issuer and recipient TINs of a submitted document, using the same
        /// self-billed rule (types 11–14 are issued by the customer) as <see cref="CanManageInvoiceAsync"/>.
        /// Cancel acts as the issuer and Reject as the recipient, so each must use that party's TIN
        /// rather than whichever company the user happens to belong to first. Returns null if the UUID is unknown.
        /// </summary>
        public static async Task<(string InvoiceNo, string? IssuerTin, string? RecipientTin)?> GetDocumentPartiesAsync(ApplicationDbContext db, string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid)) return null;

            var doc = await db.InvoiceHeaders
                .Where(i => i.UUID == uuid)
                .Select(i => new
                {
                    i.InvoiceNo,
                    SelfBilled = i.DocTypeCode == "11" || i.DocTypeCode == "12" || i.DocTypeCode == "13" || i.DocTypeCode == "14",
                    S = (string?)i.Supplier.TIN,
                    B = i.Customer != null ? i.Customer.TIN : i.PublicCustomer != null ? i.PublicCustomer.TIN : null
                })
                .FirstOrDefaultAsync();
            if (doc == null) return null;

            return doc.SelfBilled ? (doc.InvoiceNo, doc.B, doc.S) : (doc.InvoiceNo, doc.S, doc.B);
        }

    }
}
