namespace eInvWorld.Models.JsonModels
{
    public class InvoicePeriod
    {
        public List<StartDate> StartDate { get; set; } = new();
        public List<EndDate> EndDate { get; set; } = new();
        public List<Description> Description { get; set; } = new();
    }
}
