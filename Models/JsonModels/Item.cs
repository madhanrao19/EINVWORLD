namespace eInvWorld.Models.JsonModels
{
    public class Item
    {
        public List<CommodityClassification> CommodityClassification { get; set; } = new();
        public List<Description> Description { get; set; } = new();
        public List<OriginCountry> OriginCountry { get; set; } = new();

    }
}
