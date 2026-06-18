using System.ComponentModel;

namespace eInvWorld.Models
{
    public enum StatusCodes
    {
        [Description("Draft")]          // Internal status
        Draft,

        [Description("Submitted")]      // LHDN API status
        Submitted,

        [Description("Valid")]          // LHDN API status
        Valid,

        [Description("Invalid")]        // LHDN API status
        Invalid,

        [Description("Request Reject")] // Internal status
        RequestReject,

        [Description("Completed")]      // Internal status
        Completed,

        [Description("Cancelled")]      // LHDN API status
        Cancelled,

        [Description("Cancelled")]      // Internal status
        CancelledInternal
    }
}
