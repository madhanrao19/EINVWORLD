using System.ComponentModel.DataAnnotations;

namespace EINVWORLD.Models.Public
{
    public class ResourceType
    {
        [Key]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
