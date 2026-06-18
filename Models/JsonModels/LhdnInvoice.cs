namespace eInvWorld.Models.JsonModels
{
    public class LhdnInvoice
    {
        public string _D { get; set; } = null!;
        public string _A { get; set; } = null!;
        public string _B { get; set; } = null!;
        public List<Invoice> Invoice { get; set; } = new();  // Change this to List<Invoice>
    }
}
