using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eInvWorld.Models.InputModel
{
    public class Supplier
    {
        public int Id { get; set; }

        // Navigation property to represent the relationship with Buyer
        public virtual ICollection<Buyer> Buyers { get; set; } = new List<Buyer>();

        [Required(ErrorMessage = "Supplier's Name is required.")]
        [StringLength(300, ErrorMessage = "Supplier's Name cannot exceed 300 characters.")]
        [Display(Name = "Supplier Name")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Supplier's TIN is required.")]
        [StringLength(14, ErrorMessage = "Supplier's TIN must be 14 characters.")]
        [Display(Name = "Tax Identification Number")]
        public string TaxIdentificationNumber { get; set; } = null!;

        [Required(ErrorMessage = "Id Type is required.")]
        [Display(Name = "Id Type")]
        public string IdType { get; set; } = null!;

        [Required(ErrorMessage = "Supplier's Registration/Identification Number is required.")]
        [Display(Name = "Registration / Identification / Passport Number")]
        public string RegistrationIdentificationNumber { get; set; } = null!;

        [StringLength(35, ErrorMessage = "Supplier's SST Registration Number cannot exceed 35 characters.")]
        [Display(Name = "SST Registration Number")]
        public string? SSTRegistrationNumber { get; set; }

        [StringLength(17, ErrorMessage = "Supplier's Tourism Tax Registration Number cannot exceed 17 characters.")]
        [Display(Name = "Tourism Tax Registration Number")]
        public string? TourismTaxRegistrationNumber { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        [StringLength(320, ErrorMessage = "Supplier's Email cannot exceed 320 characters.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = null!;


        [Required(ErrorMessage = "MSIC Code is required.")]
        [StringLength(5, ErrorMessage = "MSIC Code must be 5 digits.")]
        [Display(Name = "MSIC Code")]
        public string MSICCode { get; set; } = null!;

        //[Required(ErrorMessage = "Business Activity Description is required.")]
        //[StringLength(300, ErrorMessage = "Business Activity Description cannot exceed 300 characters.")]
        //public string BusinessActivityDescription { get; set; }

        [Required(ErrorMessage = "Contact Number is required.")]
        [StringLength(20, ErrorMessage = "Contact Number cannot exceed 20 characters.")]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; } = null!;

        [NotMapped]
        [Display(Name = "Upload Company Logo")]
        public IFormFile? Logo { get; set; }

        [Display(Name = "Logo Path")]
        public string? LogoPath { get; set; }

        [Required(ErrorMessage = "Address Line 1 is required.")]
        [StringLength(150, ErrorMessage = "Address Line 1 cannot exceed 150 characters.")]
        [Display(Name = "Address Line 1")]
        public string AddressLine1 { get; set; } = null!;

        [StringLength(150, ErrorMessage = "Address Line 2 cannot exceed 150 characters.")]
        [Display(Name = "Address Line 2")]
        public string? AddressLine2 { get; set; }

        [StringLength(150, ErrorMessage = "Address Line 3 cannot exceed 150 characters.")]

        [Display(Name = "Address Line 3")]
        public string? AddressLine3 { get; set; }

        [StringLength(50, ErrorMessage = "Postal Zone cannot exceed 50 characters.")]
        [Display(Name = "Postal Zone")]
        public string? PostalZone { get; set; }


        [Required(ErrorMessage = "City Name is required.")]
        [StringLength(50, ErrorMessage = "City Name cannot exceed 50 characters.")]
        [Display(Name = "City Name")]
        public string CityName { get; set; } = null!;

        [Required(ErrorMessage = "State is required.")]
        [Display(Name = "State Code")]
        public string StateCode { get; set; } = null!;

        [Required(ErrorMessage = "Country code is required.")]
        [Display(Name = "Country Code")]
        public string? CountryCode { get; set; }

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // New properties for tracking assignments
        public string? AssignedBy { get; set; }
        public DateTime? AssignedDate { get; set; }

        public string? UpdatedAssignedBy { get; set; }
        public DateTime? UpdatedAssignedDate { get; set; }

        // Other properties for auditing
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsActive { get; set; } = true;


    }


}
