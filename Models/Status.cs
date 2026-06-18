using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class Status
    {
        //[Key]
        //public int StatusId { get; set; }  // Primary Key

        [Key]
        [Required]
        [MaxLength(20)]
        public string StatusCode { get; set; } = null!;  // System code (e.g., "Draft", "Valid")

        [Required]
        [MaxLength(20)]
        public string StatusType { get; set; } = null!;  // Type of status (e.g., "Internal", "LHDN")

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;  // Human-readable name (e.g., "Draft")

        [MaxLength(100)]
        public string? Description { get; set; }  // Optional description
    }
}
