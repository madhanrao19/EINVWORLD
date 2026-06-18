using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class MSICSubCategoryCode
    {
        [Key]
        [JsonProperty("Code")]
        public string Code { get; set; } = null!;

        [JsonProperty("Description")]
        public string Description { get; set; } = null!;

        [JsonProperty("MSIC Category Reference")]
        public string MSICCategoryReference { get; set; } = null!;
        public bool IsActive { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}