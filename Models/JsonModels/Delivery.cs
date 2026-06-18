namespace eInvWorld.Models.JsonModels
{
    public class Delivery
    {
        public List<DeliveryParty> DeliveryParty { get; set; } = new();
        public List<Shipment> Shipment { get; set; } = new();
    }
}
