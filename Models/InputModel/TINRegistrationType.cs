using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public enum TINRegistrationType
    {
        [Display(Name = "Identification Card No.")]
        NRIC,

        [Display(Name = "Passport No.")]
        PASSPORT,

        [Display(Name = "Business Registration No.")]
        BRN,

        [Display(Name = "Army No.")]
        ARMY,
    }

}
