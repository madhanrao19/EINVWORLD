using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class AdditionalDocumentReference
    {
        public List<ID> ID { get; set; } = new();

        // Optional - only Customs/FTA-related entries (line-level BillingReference's own
        // AdditionalDocumentReference never sets these). SkipEmptyCollectionsContractResolver omits
        // them entirely when left at their default empty list.
        public List<DocumentType> DocumentType { get; set; } = new();
        public List<DocumentDescription> DocumentDescription { get; set; } = new();
    }
}
