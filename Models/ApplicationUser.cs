using eInvWorld.Models.InputModel;
using Humanizer;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsApproved { get; set; } = false; // Default to false
        public bool IsActive { get; set; } = true;    // Indicates if the user account is active
        public bool IsDefaultUser { get; set; } = false; // Default to false; set to true for default users

        // ✅ Add Full Name
        [Required]
        public string FullName { get; set; } = string.Empty;

        // ✅ Add Profile Picture (Optional)
        public string? ProfilePicture { get; set; }

        // ✅ Replace Role with Position (e.g., "Sales Manager", "Accountant")
        public string? Position { get; set; }

        

        // ✅ One user can be assigned to multiple companies
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();


        // ✅ Add UserType to differentiate roles
        [Required]
        public string UserType { get; set; } = "User"; // "Admin", "Supplier", "Buyer", etc.

        // ✅ Add User Preferences (JSON field for storing various user-specific settings)
        public string? UserPreferences { get; set; } // JSON string to store column preferences and other settings

        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
