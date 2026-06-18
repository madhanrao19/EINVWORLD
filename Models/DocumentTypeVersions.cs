namespace eInvWorld.Models
{
    public class DocumentTypeVersions
    {
        public int id { get; set; }
        public string name { get; set; } = null!;
        public string description { get; set; } = null!;
        public DateTime? activeFrom { get; set; }
        public DateTime? activeTo { get; set; }
        public Decimal? versionNumber { get; set; }
        public string status { get; set; } = null!;
    }
}
