using System;

namespace eInvWorld.Models.InputModel
{
    public class ItemDescription
    {
        public int Id { get; set; }
        public string ClassificationCode { get; set; } = null!;
        public string ItemCode { get; set; } = null!;
        public string Description { get; set; } = null!;

        public int? CreatedByCompanyId { get; set; }
        public bool IsActive { get; set; } = true;

        // Add these new properties
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}