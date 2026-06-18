namespace eInvWorld.Models
{
    public class DocumentTypeVersion
    {
        public int id { get; set; }
        public string name { get; set; } = null!;
        public string description { get; set; } = null!;
        public DateTime? activeFrom { get; set; }
        public DateTime? activeTo { get; set; }
        public double versionNumber { get; set; }
        public string status { get; set; } = null!;
    }
}
