using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.JsonModels
{
    public class AddressLine
    {
        [NotMapped] // Exclude from EF mapping
        public List<Line> Line { get; set; } = new();

    }
}
