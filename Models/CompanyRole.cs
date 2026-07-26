using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Models
{
    /// <summary>
    /// A company-scoped permission set assignable to a <see cref="UserCompany"/> membership.
    /// Additive alongside the existing <see cref="UserCompany.HasCompanyAccess"/> /
    /// <see cref="UserCompany.IsViewOnly"/> flags — those keep working for rows with no role
    /// assigned; a role, once set, is the source of truth for what the tabs below let a user do.
    /// </summary>
    public class CompanyRole
    {
        [Key]
        public int CompanyRoleId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;

        public bool CanManageUsers { get; set; }
        public bool CanEditProfile { get; set; }
        public bool CanManageBranding { get; set; }
        public bool CanViewAudit { get; set; }

        /// <summary>True for the four seeded roles (Owner/Admin/Editor/Viewer) — not user-deletable.</summary>
        public bool IsSystemDefined { get; set; }

        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
    }
}
