namespace eInvWorld.Models.JsonModels
{
    public class Root
    {
        public string _D { get; set; } = null!;
        public string _A { get; set; } = null!;
        public string _B { get; set; } = null!;
        public List<Invoice> Invoice { get; set; } = new();
    }
}
