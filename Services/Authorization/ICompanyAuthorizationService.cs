using System.Threading;
using System.Threading.Tasks;

namespace EINVWORLD.Services.Authorization
{
    public enum CompanyPermission
    {
        ManageUsers,
        EditProfile,
        ManageBranding,
        ViewAudit,
    }

    /// <summary>
    /// Resolves whether a user may perform a company-scoped action on a given PartyInfo (company).
    /// Every Company workspace tab's write handlers must route through this rather than re-deriving
    /// access from raw UserCompany flags, so the permission model stays in exactly one place.
    /// </summary>
    public interface ICompanyAuthorizationService
    {
        Task<bool> HasPermissionAsync(string userId, int partyInfoId, CompanyPermission permission, CancellationToken ct = default);
    }
}
