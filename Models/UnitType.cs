using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class UnitType
    {
        [Key]
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}