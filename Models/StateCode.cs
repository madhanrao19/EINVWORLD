using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class StateCode
    {
        [Key]
        public string Code { get; set; } = null!;
        public string State { get; set; } = null!;
        public bool IsActive { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}