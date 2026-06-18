using System;

namespace eInvWorld.Models.JsonModels
{
    public class InvoiceDocumentReference
    {
        public List<ID> ID { get; set; } = new();
        public List<UUID> UUID { get; set; } = new();
    }
}
