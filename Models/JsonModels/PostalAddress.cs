using eInvWorld.Models.JsonModels;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.JsonModels
{
    public class PostalAddress
    {
        [NotMapped] // Prevent mapping to the database
        public List<CityName> CityName { get; set; } = new();

        [NotMapped]
        public List<PostalZone> PostalZone { get; set; } = new();

        [NotMapped]
        public List<CountrySubentityCode> CountrySubentityCode { get; set; } = new();

        [NotMapped]
        public List<AddressLine> AddressLine { get; set; } = new();

        [NotMapped]
        public List<Country> Country { get; set; } = new();
    }
}
