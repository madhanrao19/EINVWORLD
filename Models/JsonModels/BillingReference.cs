namespace eInvWorld.Models.JsonModels
{
    public class BillingReference
    {
        public List<InvoiceDocumentReference> InvoiceDocumentReference { get; set; } = new();
        public List<AdditionalDocumentReference> AdditionalDocumentReference { get; set; } = new();

    }
}
