using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using System.Threading.Tasks;
using System.Linq;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Company gate for the Supplier pages (items, buyers, invoices, templates, company settings).
    /// It authorises against the company the request actually reads or changes, never a generic query
    /// string: by default the user's acting company (<see cref="ActingCompany"/>, the one these handlers
    /// write to); pages that act on a company named in the request list the handler arguments that carry it
    /// in <see cref="TargetCompanyArguments"/>.
    /// </summary>
    public class SupplierBasePage : PageModel
    {
        protected readonly ApplicationDbContext _context;

        public SupplierBasePage(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Handler arguments (in order) that name the company a handler acts on, e.g. "id" or "partyInfoId".
        /// Empty (the default): the page acts on the user's acting company.
        /// </summary>
        protected virtual string[] TargetCompanyArguments => System.Array.Empty<string>();

        /// <summary>
        /// The user's memberships in the order every "my company" lookup uses: the primary company first,
        /// then the oldest membership (the order SQL Server returned ties in before), so the gate and the
        /// handler always pick the same company.
        /// </summary>
        public static IQueryable<UserCompany> ActingCompany(ApplicationDbContext db, string? userId) =>
            db.UserCompanies.Where(uc => uc.UserId == userId)
                .OrderByDescending(uc => uc.IsPrimaryCompany).ThenBy(uc => uc.Id);

        /// <summary>
        /// May this user open this page for <paramref name="targetCompanyId"/> (null = their acting company)?
        /// Admins always may. Others need a membership of that exact company with access; a view-only
        /// membership only reaches the dashboard (view-only members may only view, print and download).
        /// </summary>
        public static async Task<bool> MayEnterAsync(ApplicationDbContext db, string? userId, bool isAdmin, int? targetCompanyId, string path)
        {
            if (isAdmin) return true;
            if (string.IsNullOrEmpty(userId)) return false;

            var membership = targetCompanyId is int id
                ? await db.UserCompanies.AsNoTracking().FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == id)
                : await ActingCompany(db, userId).AsNoTracking().FirstOrDefaultAsync();

            if (membership == null) return false;
            if (membership.IsViewOnly) return path.ToLowerInvariant().Contains("/dashboard");
            return membership.HasCompanyAccess || membership.IsPrimaryCompany;
        }

        public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            int? target = null;
            foreach (var name in TargetCompanyArguments)
            {
                if (context.HandlerArguments.TryGetValue(name, out var value) && value is int id)
                {
                    target = id;
                    break;
                }
            }

            var allowed = await MayEnterAsync(_context, User.FindFirstValue(ClaimTypes.NameIdentifier), User.IsInRole("Admin"),
                target, context.HttpContext.Request.Path.Value ?? "");
            if (!allowed)
            {
                context.Result = Redirect("/Identity/Account/AccessDenied");
                return;
            }
            await next.Invoke();
        }
    }
}
