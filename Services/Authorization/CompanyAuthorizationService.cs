using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Authorization
{
    /// <summary>
    /// Default <see cref="ICompanyAuthorizationService"/>. Does NOT special-case the global "Admin"
    /// Identity role — callers that already bypass tenant checks for Admins (the established pattern
    /// throughout Pages/Suppliers and Pages/PublicCustomer) should keep doing that themselves before
    /// falling back to this service for the company-membership case.
    /// </summary>
    public sealed class CompanyAuthorizationService : ICompanyAuthorizationService
    {
        private readonly ApplicationDbContext _context;

        public CompanyAuthorizationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermissionAsync(string userId, int partyInfoId, CompanyPermission permission, CancellationToken ct = default)
        {
            var membership = await _context.UserCompanies
                .Include(uc => uc.CompanyRole)
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == partyInfoId, ct);

            if (membership == null)
                return false;

            if (membership.CompanyRole != null)
            {
                return permission switch
                {
                    CompanyPermission.ManageUsers => membership.CompanyRole.CanManageUsers,
                    CompanyPermission.EditProfile => membership.CompanyRole.CanEditProfile,
                    CompanyPermission.ManageBranding => membership.CompanyRole.CanManageBranding,
                    CompanyPermission.ViewAudit => membership.CompanyRole.CanViewAudit,
                    _ => false,
                };
            }

            // No role assigned yet — fall back to the legacy two-tier flags so existing memberships
            // keep working exactly as before until someone explicitly assigns a role.
            if (!membership.HasCompanyAccess)
                return false;

            return permission switch
            {
                CompanyPermission.ViewAudit => true,
                _ => !membership.IsViewOnly,
            };
        }
    }
}
