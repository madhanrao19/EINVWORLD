using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    public class SupplierBuyer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        public int? BuyerId { get; set; }

        public int? PublicCustomerId { get; set; }

        // Relationships
        [ForeignKey("SupplierId")]
        public PartyInfo Supplier { get; set; } = null!;

        [ForeignKey("BuyerId")]
        public PartyInfo? Buyer { get; set; } // Make nullable

        // Add Navigation Property
        [ForeignKey("PublicCustomerId")]
        public PublicCustomer? PublicCustomer { get; set; }
    }
}