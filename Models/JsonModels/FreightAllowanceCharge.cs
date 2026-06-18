namespace eInvWorld.Models.JsonModels
{
    public class FreightAllowanceCharge
    {
        public List<ChargeIndicator> ChargeIndicator { get; set; } = new();
        public List<AllowanceChargeReason> AllowanceChargeReason { get; set; } = new();
        public List<Amount> Amount { get; set; } = new();
    }
}
