using eInvWorld.Models.InputModel;

namespace eInvWorld.Services
{
    /// <summary>Sends e-invoice lifecycle notification emails (validated / rejected / cancelled).
    /// Behind an interface so consumers depend on the abstraction (DIP) and it can be mocked in tests.</summary>
    public interface IEInvoiceNotificationService
    {
        Task SendValidatedNotificationEmail(string recipientName, PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, DateTime validatedTimestamp, PublicCustomer? publicCustomer = null);
        void SendRejectionNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string rejectionReason, DateTime rejectedTimestamp);
        void SendCancellationNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string cancellationReason, DateTime cancelledTimestamp);

        /// <summary>Notifies the buyer that a new e-invoice from a supplier was just discovered via
        /// LHDN sync (submitted directly to LHDN by the supplier's own system, not through EINVWORLD).
        /// Buyer-only — the supplier already knows they sent it.
        /// Returns <c>false</c> (not an error — nothing to retry) when there's no valid buyer email to
        /// send to. Throws on an actual send failure (SMTP, etc.) so the caller (InvoiceFinalizer) can
        /// roll back its atomic claim and retry on the next background pass.</summary>
        Task<bool> SendNewInvoiceReceivedNotificationEmail(PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, PublicCustomer? publicCustomer = null);
    }
}
