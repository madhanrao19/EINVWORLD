using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class InvoiceLine
    {
        public List<AllowanceCharge> AllowanceCharge { get; set; } = new();
        public List<ID> ID { get; set; } = new();
        public List<InvoicedQuantity> InvoicedQuantity { get; set; } = new();
        public List<Item> Item { get; set; } = new();
        public List<ItemPriceExtension> ItemPriceExtension { get; set; } = new();
        public List<LineExtensionAmount> LineExtensionAmount { get; set; } = new();
        public List<Price> Price { get; set; } = new();
        public List<TaxTotal>? TaxTotal { get; set; }
    }
}
