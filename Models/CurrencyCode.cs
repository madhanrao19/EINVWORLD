using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class CurrencyCode
    {
        [Key]
        public string Code { get; set; } = null!;
        public string Currency { get; set; } = null!;
        public bool IsActive { get; set; }
        public string UpdatedBy { get; set; } = null!;
        public DateTime? UpdatedDate { get; set; }

        public CurrencyCode() { }

        public CurrencyCode(string code, string currency, bool isActive, string? updatedBy = null, DateTime? updatedDate = null)
        {
            Code = code;
            Currency = currency;
            IsActive = isActive;
            UpdatedBy = updatedBy ?? "";
            UpdatedDate = updatedDate;
        }

        public Dictionary<string, object> ToMap()
        {
            return new Dictionary<string, object>
            {
                { "Code", Code },
                { "Currency", Currency },
                { "UpdatedBy", UpdatedBy ?? (object)DBNull.Value },
                { "UpdatedDate", UpdatedDate ?? (object)DBNull.Value }
            };
        }
    }
}
