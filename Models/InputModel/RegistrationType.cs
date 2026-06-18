using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public class RegistrationType
    {

        [Key]
        [StringLength(10)]  // Short codes like "NRIC", "BRN"
        public string Code { get; set; } = null!;  // ✅ Stores NRIC, BRN, etc.

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;  // ✅ Stores "Identification Card No."
    }
}
