using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eInvWorld.Models.JsonModels
{
    public class InvoiceApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, InvoiceField> Fields { get; set; } = new();

        [JsonPropertyName("line_items")]
        public List<ExtractedLineItem> LineItems { get; set; } = new();

        public string? GetFieldString(string key)
        {
            if (Fields == null || !Fields.TryGetValue(key, out var field) || field == null)
                return null;

            return field.GetStringValue();
        }

        public decimal? GetFieldDecimal(string key)
        {
            if (Fields == null || !Fields.TryGetValue(key, out var field) || field == null)
                return null;

            return field.GetDecimalValue();
        }
    }

    public class InvoiceField
    {
        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        public string? GetStringValue()
        {
            return Value.ValueKind switch
            {
                JsonValueKind.String => Value.GetString(),
                JsonValueKind.Number => Value.GetDecimal().ToString("0.##"),
                JsonValueKind.Null => null,
                _ => Value.ToString()
            };
        }

        public decimal? GetDecimalValue()
        {
            if (Value.ValueKind == JsonValueKind.Number && Value.TryGetDecimal(out var d))
                return d;

            if (Value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(Value.GetString(), out var parsed))
                return parsed;

            return null;
        }
    }

    public class ExtractedLineItem
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = null!;

        [JsonPropertyName("qty")]
        public decimal? Qty { get; set; }

        [JsonPropertyName("quantity")]
        public decimal? Quantity { get; set; }

        [JsonPropertyName("unit_price")]
        public decimal? UnitPrice { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("total_amount")]
        public decimal? TotalAmount { get; set; }
    }
}