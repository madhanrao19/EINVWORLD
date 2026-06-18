using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class EmailNotification
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Template Name")]
        public string TemplateName { get; set; } = null!;

        [Required]
        [StringLength(250)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = null!;

        [Required]
        [StringLength(2000)]
        [Display(Name = "Body")]
        public string Body { get; set; } = null!;

        [Required]
        [Display(Name = "Notification Type")]
        public NotificationType NotificationType { get; set; }

        // Property to toggle the notification on/off
        [Display(Name = "Notification Active?")]
        public bool IsActive { get; set; } = false; // Default is false (inactive)

        // Property for storing the creation date
        [DataType(DataType.DateTime)]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now; // Automatically set to now when created

        // Property for storing the last updated date
        [DataType(DataType.DateTime)]
        [Display(Name = "Updated Date")]
        public DateTime UpdatedDate { get; set; } = DateTime.Now; // Automatically set to now when updated
    }

    public enum NotificationType
    {
        [Display(Name = "Approval Request")]
        ApprovalRequest,

        [Display(Name = "Alert")]
        Alert,

        [Display(Name = "Reminder")]
        Reminder,

        [Display(Name = "Confirmation")]
        Confirmation,
        // Add other types as needed
    }
}
