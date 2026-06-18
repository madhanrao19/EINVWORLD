using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class CountryCode
    {
        [Key]
        public string Code { get; set; } = null!;
        public string Country { get; set; } = null!;
        public bool IsActive { get; set; }
        public string UpdatedBy { get; set; } = null!;
        public DateTime? UpdatedDate { get; set; }
    }
}