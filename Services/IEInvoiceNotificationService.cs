using eInvWorld.Models.InputModel;

namespace eInvWorld.Services
{
    /// <summary>Sends e-invoice lifecycle notification emails (validated / rejected / cancelled).
    /// Behind an interface so consumers depend on the abstraction (DIP) and it can be mocked in tests.</summary>
    public interface IEInvoiceNotificationService
    {
        Task SendValidatedNotificationEmail(string recipientName, PartyInfo? buyer, PartyInfo? supplier, string documentId, DateTime issueDate, DateTime validatedTimestamp);
        void SendRejectionNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string rejectionReason, DateTime rejectedTimestamp);
        void SendCancellationNotificationEmail(PartyInfo buyer, PartyInfo supplier, string documentId, string cancellationReason, DateTime cancelledTimestamp);
    }
}
