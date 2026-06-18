using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.JsonModels
{
    [NotMapped]
    public class DeliveryParty
    {
        public List<PartyLegalEntity> PartyLegalEntity { get; set; } = new();
        public List<PostalAddress> PostalAddress { get; set; } = new();
        public List<PartyIdentification> PartyIdentification { get; set; } = new();
    }
}
