using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class Shipment
    {
        public List<ID> ID { get; set; } = new();
        public List<FreightAllowanceCharge> FreightAllowanceCharge { get; set; } = new();
    }
}
