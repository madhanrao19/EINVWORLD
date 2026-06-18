using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public enum InvoicePeriodEnum
    {
        Daily,
        Weekly,
        Biweekly,
        Monthly,
        Bimonthly,
        Quarterly,
        [Display(Name = "Half-yearly")]
        Half_Yearly,
        Yearly,
        Others,
        [Display(Name = "Not Applicable")]
        Not_Applicable,
    }
}
