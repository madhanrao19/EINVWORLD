using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using eInvWorld.Data;
using System.Threading.Tasks;
using System.Linq;

namespace EINVWORLD.Helpers
{
    public class SupplierBasePage : PageModel
    {
        protected readonly ApplicationDbContext _context;

        public SupplierBasePage(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            // 1. Admin Bypass
            if (User.IsInRole("Admin"))
            {
                await next.Invoke();
                return;
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int companyId = 0;

            // 2. Identify Company Context
            if (int.TryParse(Request.Query["id"], out int queryId))
            {
                companyId = queryId;
            }
            else
            {
                // Fallback: If no Primary, find *any* company they are linked to
                var primary = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.IsPrimaryCompany);

                if (primary != null)
                {
                    companyId = primary.PartyInfoId;
                }
                else
                {
                    var anyCompany = await _context.UserCompanies
                        .FirstOrDefaultAsync(uc => uc.UserId == userId);

                    if (anyCompany != null) companyId = anyCompany.PartyInfoId;
                }
            }

            // 3. Fetch Permissions
            var permission = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == companyId);

            if (permission == null)
            {
                context.Result = Redirect("/Identity/Account/AccessDenied");
                return;
            }

            // 4. SECURITY LOGIC (Strict Order)

            // ✅ PRIORITY 1: CHECK VIEW ONLY
            if (permission.IsViewOnly)
            {
                string currentPath = context.HttpContext.Request.Path.Value?.ToLower() ?? "";

                // ✅ STRICT SAFE LIST
                // Only allow Dashboard and Invoice Creation. 
                // Any other page will trigger Access Denied.
                bool isSafe = currentPath.Contains("/dashboard") ||
                              currentPath.Contains("/einvoice");

                if (isSafe)
                {
                    await next.Invoke(); // Allow access
                    return;
                }
                else
                {
                    // ⛔ BLOCK: Redirect to standard Access Denied page
                    context.Result = Redirect("/Identity/Account/AccessDenied");
                    return;
                }
            }

            // ✅ PRIORITY 2: FULL ACCESS
            if (permission.HasCompanyAccess || permission.IsPrimaryCompany)
            {
                await next.Invoke(); // Allow everything
                return;
            }

            // ✅ PRIORITY 3: NO PERMISSION
            // If neither checkbox is ticked (0,0), BLOCK.
            context.Result = Redirect("/Identity/Account/AccessDenied");
        }
    }
}