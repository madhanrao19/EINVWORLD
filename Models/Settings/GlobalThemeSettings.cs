using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.Settings
{
    public class GlobalThemeSettings
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string DataLayout { get; set; } = "vertical";
        
        [Required]
        public string DataTheme { get; set; } = "default";
        
        [Required]
        public string DataThemeColors { get; set; } = "green";
        
        [Required]
        public string DataTopbar { get; set; } = "light";
        
        [Required]
        public string DataSidebar { get; set; } = "dark";
        
        [Required]
        public string DataSidebarSize { get; set; } = "lg";
        
        [Required]
        public string DataSidebarImage { get; set; } = "none";
        
        [Required]
        public string DataLayoutWidth { get; set; } = "fluid";
        
        [Required]
        public string DataLayoutPosition { get; set; } = "fixed";
        
        [Required]
        public string DataLayoutStyle { get; set; } = "default";
        
        [Required]
        public string DataBsTheme { get; set; } = "light";
        
        [Required]
        public string DataPreloader { get; set; } = "disable";
        
        [Required]
        public string DataBodyImage { get; set; } = "none";
        
        [Required]
        public string DataSidebarVisibility { get; set; } = "show";
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public string? UpdatedBy { get; set; } // Admin user ID who last updated
    }
}