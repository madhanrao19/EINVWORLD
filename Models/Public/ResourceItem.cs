using System.ComponentModel.DataAnnotations;
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


    }
}
