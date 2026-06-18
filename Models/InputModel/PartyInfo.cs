using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Eventing.Reader;
using EINVWORLD.Helpers.Validation;


namespace eInvWorld.Models.InputModel
{
    public class PartyInfo
    {
        [Key]
        public int PartyInfoId { get; set; }  // Primary key

        [Display(Name = "MSIC Code")]
        public string IndustryClassificationCode { get; set; } = null!;

        [StringLength(300, ErrorMessage = "Business Activity Description cannot exceed 300 characters.")]
        public string BizDescription { get; set; } = "";

        [Required(ErrorMessage = "Supplier's Name is required.")]
        [StringLength(300, ErrorMessage = "Supplier's Name cannot exceed 300 characters.")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = null!;

        [Required(ErrorMessage = "Supplier's TIN is required.")]
        [StringLength(14, ErrorMessage = "Supplier's TIN must be 14 characters.")]
        [Display(Name = "Tax Identification Number")]
        public string TIN { get; set; } = null!;

        [Required]
        [StringLength(10)]
        public string RegTypeCode { get; set; } = null!; // Stores "NRIC", "BRN", etc.

        [ForeignKey("RegTypeCode")]
        public RegistrationType? RegType { get; set; } // Navigation Property

        [Required(ErrorMessage = "Supplier's Registration/Identification Number is required.")]
        [Display(Name = "Registration Number")]
        public string RegNo { get; set; } = null!;

        [Display(Name = "Old Registration Number")]
        [StringLength(20)]
        public string? OldRegNo { get; set; }

        [RequiredOrNA(ErrorMessage = "SST is required or enter 'NA'")]
        [StringLength(35, ErrorMessage = "Supplier's SST Registration Number cannot exceed 35 characters.")]
        [Display(Name = "SST Registration Number")]
        public string? SST { get; set; }

        [RequiredOrNA(ErrorMessage = "TTX is required or enter 'NA'")]
        [StringLength(17, ErrorMessage = "Supplier's Tourism Tax Registration Number cannot exceed 17 characters.")]
        [Display(Name = "Tourism Tax Registration Number")]
        public string? TTX { get; set; }

        [Required(ErrorMessage = "Address Line 1 is required.")]
        [StringLength(150, ErrorMessage = "Address Line 1 cannot exceed 150 characters.")]
        [Display(Name = "Address Line 1")]
        public string Addr1 { get; set; } = null!;


        [StringLength(150, ErrorMessage = "Address Line 2 cannot exceed 150 characters.")]
        [Display(Name = "Address Line 2")]
        public string? Addr2 { get; set; }


        [StringLength(150, ErrorMessage = "Address Line 3 cannot exceed 150 characters.")]

        [Display(Name = "Address Line 3")]
        public string? Addr3 { get; set; }

        [StringLength(50, ErrorMessage = "Postal Zone cannot exceed 50 characters.")]
        [Display(Name = "Postal Zone")]
        public string? PostalCode { get; set; }


        [Required(ErrorMessage = "City Name is required.")]
        [StringLength(50, ErrorMessage = "City Name cannot exceed 50 characters.")]
        [Display(Name = "City Name")]
        public string CityName { get; set; } = null!;

        [Required(ErrorMessage = "State is required.")]
        [Display(Name = "State Code")]
        public string StateCode { get; set; } = null!;

         [ForeignKey("StateCode")]
        public virtual StateCode? State { get; set; } // ✅ Navigation Property

        [Required(ErrorMessage = "Country code is required.")]
        [Display(Name = "Country Code")]
        public string CountryCode { get; set; } = null!;

        [ForeignKey("CountryCode")]
        public virtual CountryCode? Country { get; set; } // ✅ Navigation Property

        [EmailAddress(ErrorMessage = "Invalid Email Address.")]
        [StringLength(320, ErrorMessage = "Supplier's Email cannot exceed 320 characters.")]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [RegularExpression(@"^\+[0-9]{8,14}$", ErrorMessage = "Phone number must start with '+' and contain only digits (e.g., +60376602222).")]
        [Required(ErrorMessage = "Contact Number is required.")]
        [StringLength(20, ErrorMessage = "Contact Number cannot exceed 20 characters.")]
        [Display(Name = "Contact Number")]
        public string PhoneNo { get; set; } = null!;

        [RegularExpression(@"^\+[0-9]{8,14}$", ErrorMessage = "Fax number must start with '+' and contain only digits (e.g., +60376602222).")]

        [StringLength(20, ErrorMessage = "Fax Number cannot exceed 20 characters.")]
        [Display(Name = "Fax Number")]
        public string? FaxNo { get; set; }

        [Display(Name = "Bank Account Number")]
        [StringLength(150, ErrorMessage = "Bank Account Number cannot exceed 150 characters.")]
        public string? BankAccountNo { get; set; }

        [Display(Name = "Bank Name")]
        [StringLength(100, ErrorMessage = "Bank Name cannot exceed 100 characters.")]
        public string? BankName { get; set; }

        [Display(Name = "Attention To")]
        [StringLength(200, ErrorMessage = "Attention To cannot exceed 200 characters.")]
        public string? Attention { get; set; }

        [Display(Name = "Payment Terms")]
        [StringLength(300, ErrorMessage = "Payment Terms cannot exceed 300 characters.")]
        public string? PaymentTerms { get; set; }

        [Display(Name = "Authorisation Number for Certified Exporter")]
        [StringLength(300, ErrorMessage = "Authorisation Number cannot exceed 300 characters.")]
        public string? AuthorisationNumber { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // ✅ Add LogoPath (Nullable)
        public string? LogoPath { get; set; }
        // ✅ Timestamps for auditing
        [Required]
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Created By")]
        public string CreatedBy { get; set; } = null!;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        [StringLength(8)]
        public string? InviteCode { get; set; } // Make nullable

        public bool IsApproved { get; set; } = false;


        public bool IsAdminCreated { get; set; } // Flag to check who is creating the company

        //public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        //{
        //    // If this is a user registration, require InviteCode
        //    if (!IsAdminCreated && string.IsNullOrEmpty(InviteCode))
        //    {
        //        yield return new ValidationResult("Invite Code is required for new user registration.", new[] { "InviteCode" });
        //    }
        //}

        [Display(Name = "LHDN Intermediary Rejected")]
        public bool LhdnIntermediaryRejected { get; set; } = false;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!IsAdminCreated && string.IsNullOrEmpty(InviteCode))
            {
                // Skip invite code check if from public submit
                var actionContext = validationContext.GetService(typeof(ActionContext)) as ActionContext;
                var httpContext = (validationContext.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor)?.HttpContext;

                // Allow if accessed via public path
                var path = httpContext?.Request?.Path.Value?.ToLower() ?? "";
                if (path.Contains("/publiccustomer/submit")) yield break;

                yield return new ValidationResult("Invite Code is required for new user registration.", new[] { "InviteCode" });
            }
        }



        // ✅ Navigation: Suppliers can have multiple Buyers
        public ICollection<SupplierBuyer> AssignedBuyers { get; set; } = new List<SupplierBuyer>();

        // ✅ Navigation: Buyers can have multiple Suppliers
        public ICollection<SupplierBuyer> AssignedSuppliers { get; set; } = new List<SupplierBuyer>();

        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();


    }
}
