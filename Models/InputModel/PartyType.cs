using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public enum PartyType
    {
        [Display(Name = "Supplier")]
        Supplier = 1,  // Set this to 1 if Supplier records have Type = 1 in the database

        [Display(Name = "Customer")]
        Customer = 2   // Set this to 2 if Customer records have Type = 2 in the database
    }
}
