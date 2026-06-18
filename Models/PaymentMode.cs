using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class PaymentMode
    {
        [Key]
        [JsonProperty("Code")]
        public string Code { get; set; } = null!;

        [JsonProperty("Payment Method")]
        public string PaymentMethod { get; set; } = null!;
        public bool IsActive { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}