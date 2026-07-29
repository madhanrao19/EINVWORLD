using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        /// <summary>Null for a system-wide role visible to every company. Set to scope a custom role
        /// (created by that company's own Owner/Admin) to just that one company.</summary>
        public int? PartyInfoId { get; set; }

        [ForeignKey(nameof(PartyInfoId))]
        public PartyInfo? PartyInfo { get; set; }

        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
    }
}
