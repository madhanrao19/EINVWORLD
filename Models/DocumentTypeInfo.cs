namespace eInvWorld.Models
{
    public class DocumentTypeInfo
    {
        public int id { get; set; }
        public int invoiceTypeCode { get; set; }
        public string description { get; set; } = null!;
        public DateTime? activeFrom { get; set; }
        public DateTime? activeTo { get; set; }
        public List<DocumentTypeVersions> documentTypeVersions { get; set; } = new();
        public List<WorkflowParameter> workflowParameters { get; set; } = new();
    }
}
