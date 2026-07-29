using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    /// <summary>
    /// Whether a global Identity role (Supplier/Buyer — Admin always has full access) may access a
    /// given module. Additive on top of the existing per-page <c>[Authorize(Roles=...)]</c> gates:
    /// those still decide who can even reach the page; a missing row here defaults to allowed, so
    /// nothing regresses until an admin explicitly restricts a module for a role.
    /// </summary>
    public class RoleModulePermission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string RoleName { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string ModuleKey { get; set; } = null!;

        public bool IsAllowed { get; set; } = true;
    }
}
