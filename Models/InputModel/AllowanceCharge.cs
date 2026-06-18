using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public class AllowanceCharge
    {
        [Key]  // ✅ Add this line to define the primary key
        public int Id { get; set; }  // Primary Key
        public bool IsCharge { get; set; }  // ➡️ Represents ChargeIndicator (true/false)
        public string Reason { get; set; } = null!;  // ➡️ Represents AllowanceChargeReason
        public decimal Amount { get; set; } // ➡️ Represents Amount
        public decimal MultiplierFactor { get; set; }  // ➡️ Represents MultiplierFactorNumeric
    }
}
