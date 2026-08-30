using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Models.Public
{
    [Index(nameof(Slug), IsUnique = true)]
    public class ResourceItem
    {
        public int Id { get; set; }

        [StringLength(200)]
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string ImageUrl { get; set; } = "/images/resources/default.jpg";
        public string ThumbnailUrl { get; set; } = "/images/resources/default-thumb.jpg";
        public DateTime DatePublished { get; set; }
        public string ContentHtml { get; set; } = string.Empty;

        public ResourceStatus Status { get; set; } = ResourceStatus.Draft;

        [Required]
        [StringLength(50)]
        public string ResourceTypeCode { get; set; } = "";  // Foreign key to ResourceType.Code

        [ValidateNever]  // Skip validation on navigation property
        public ResourceType? ResourceType { get; set; }

        // ── SEO / GEO (answer-engine) metadata ─────────────────────────────────────────────
        [StringLength(60)]
        public string? MetaTitle { get; set; }

        [StringLength(160)]
        public string? MetaDescription { get; set; }

        [StringLength(100)]
        public string? FocusKeyword { get; set; }

        [StringLength(500)]
        public string? CanonicalUrl { get; set; }

        /// <summary>Social share text; falls back to MetaDescription when blank.</summary>
        [StringLength(200)]
        public string? OgText { get; set; }

        [StringLength(200)]
        public string? ImageAlt { get; set; }

        [StringLength(100)]
        public string? Author { get; set; }

        /// <summary>1-2 sentence answer-first summary written for AI assistants to quote (GEO).</summary>
        [StringLength(400)]
        public string? Tldr { get; set; }

        public ResourceSchemaType SchemaType { get; set; } = ResourceSchemaType.Article;

        /// <summary>Raw JSON array of ResourceFaqItem, used only when SchemaType == FAQ. Use the
        /// FaqItems accessor to read/write typed values; the column stays a plain string so no
        /// EF JSON-column configuration is required.</summary>
        public string? FaqItemsJson { get; set; }

        [NotMapped]
        [ValidateNever]
        [System.Text.Json.Serialization.JsonIgnore]
        public List<ResourceFaqItem> FaqItems
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FaqItemsJson)) return new List<ResourceFaqItem>();
                try
                {
                    return JsonSerializer.Deserialize<List<ResourceFaqItem>>(FaqItemsJson!) ?? new List<ResourceFaqItem>();
                }
                catch (JsonException)
                {
                    return new List<ResourceFaqItem>();
                }
            }
            set => FaqItemsJson = value == null || value.Count == 0 ? null : JsonSerializer.Serialize(value);
        }
    }
}
