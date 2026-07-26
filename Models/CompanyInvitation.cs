using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Models
{
    /// <summary>
    /// A pending invite to join a company's workspace. Only ever carries an email + intended role —
    /// never a password. The invitee always sets their own password via the normal Identity flow
    /// when accepting (AcceptInvite page), whether or not they already have an EINVWORLD account.
    /// </summary>
    public class CompanyInvitation
    {
        [Key]
        public int CompanyInvitationId { get; set; }

        [Required]
        public int PartyInfoId { get; set; }

        [ForeignKey("PartyInfoId")]
        public PartyInfo PartyInfo { get; set; } = null!;

        [Required]
        [StringLength(320)]
        public string Email { get; set; } = null!;

        [Required]
        public string InvitedByUserId { get; set; } = null!;

        public int? CompanyRoleId { get; set; }

        [ForeignKey("CompanyRoleId")]
        public CompanyRole? CompanyRole { get; set; }

        /// <summary>SHA-256 hash of the raw token mailed to the invitee — the raw value is never stored.</summary>
        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
