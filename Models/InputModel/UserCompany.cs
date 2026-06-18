using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public class UserCompany
    {
        [Key]
        public int Id { get; set; } // Primary key

        [Required]
        public string UserId { get; set; } = null!; // Foreign Key for User

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int PartyInfoId { get; set; } // Foreign Key for Company

        [ForeignKey("PartyInfoId")]
        public PartyInfo PartyInfo { get; set; } = null!;

        public bool IsPrimaryCompany { get; set; } = false; // ✅ To mark a default company (Optional)
        public bool HasCompanyAccess { get; set; } = false;
        public bool IsViewOnly { get; set; } = false;
    }
}
