using eInvWorld.Data;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Authorization
{
    /// <summary>
    /// Global Razor Pages filter enforcing the Role Management module grid. Runs after the page's own
    /// <c>[Authorize(Roles=...)]</c> — that attribute remains the outer gate deciding who can reach the
    /// page at all; this filter adds a second, admin-configurable check on top of it: does the current
    /// user's role have this module enabled? Admin always passes. A user with no Supplier/Buyer role,
    /// or a module with no configured row, is allowed through unchanged (fail-open, additive).
    /// </summary>
    public class ModuleAccessPageFilter : IAsyncPageFilter
    {
        private static readonly string[] GatedRoles = { "Supplier", "Buyer" };

        private readonly ApplicationDbContext _context;

        public ModuleAccessPageFilter(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true || user.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var module = RoleModules.ForPath(context.HttpContext.Request.Path.Value ?? "");
            if (module == null)
            {
                await next();
                return;
            }

            var userRoles = GatedRoles.Where(user.IsInRole).ToList();
            if (userRoles.Count == 0)
            {
                await next();
                return;
            }

            // Allowed if ANY of the user's roles grants this module (missing row = default allow).
            var restrictions = await _context.RoleModulePermissions
                .Where(p => userRoles.Contains(p.RoleName) && p.ModuleKey == module.Key)
                .ToListAsync();

            bool anyRoleAllowed = userRoles.Any(role =>
            {
                var row = restrictions.FirstOrDefault(r => r.RoleName == role);
                return row == null || row.IsAllowed;
            });

            if (!anyRoleAllowed)
            {
                context.Result = new RedirectResult("/Identity/Account/AccessDenied");
                return;
            }

            await next();
        }
    }
}
