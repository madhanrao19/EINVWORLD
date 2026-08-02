using System.Threading;
using System.Threading.Tasks;

namespace eInvWorld.Services
{
    /// <summary>
    /// Outcome of a single-invoice finalization pass (see <see cref="IInvoiceFinalizer"/>).
    /// </summary>
    public sealed class InvoiceFinalizeResult
    {
        /// <summary>The invoice met the finalization preconditions (Valid + LongId + DateTimeValidated).</summary>
        public bool Eligible { get; init; }

        /// <summary>A PDF was generated during this pass.</summary>
        public bool PdfGenerated { get; init; }

        /// <summary>The validation email was sent during this pass.</summary>
        public bool EmailSent { get; init; }

        /// <summary>Nothing left to do: PDF exists and the email has been sent.</summary>
        public bool Completed { get; init; }
    }

    /// <summary>
    /// Finalizes a validated invoice: generates the PDF (with QR) if missing, then sends the
    /// validation email to Supplier/Buyer (+ global BCC) exactly once. This is the single shared
    /// implementation used by the interactive submit flow and every background finalizer loop, so
    /// concurrent callers cannot double-send the email (the send is guarded by an atomic claim on
    /// <c>IsValidationEmailSent</c>).
    /// </summary>
    public interface IInvoiceFinalizer
    {
        /// <summary>
        /// Runs the PDF + email finalization for one invoice. Safe to call for an invoice in any
        /// state — it no-ops unless the invoice is Valid with its LongId and validation timestamp
        /// present. Failures are logged and reflected in the result; they are never thrown, so
        /// callers can treat this as best-effort (a later finalizer pass retries anything pending).
        /// </summary>
        /// <param name="invoiceNo">The invoice number to finalize.</param>
        /// <param name="performedBy">Actor recorded in the invoice history entries.</param>
        /// <param name="cancellationToken">Cancellation for the DB operations.</param>
        Task<InvoiceFinalizeResult> FinalizeInvoiceAsync(string invoiceNo, string performedBy = "Finalizer", CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the "new e-invoice received" notification for a buyer-side invoice synced in from an
        /// external ERP, if still eligible (<c>IsNewInvoiceReceivedEmailSent == false</c>). Same
        /// atomic-claim-and-rollback-on-failure pattern as the Valid-status email in
        /// <see cref="FinalizeInvoiceAsync"/>, so a later background pass retries a failed send. An
        /// invoice past the configured recency window, or with the feature disabled, is claimed and
        /// marked done without sending — it will not be retried once expired/disabled.
        /// </summary>
        Task<bool> SendNewInvoiceReceivedEmailAsync(string invoiceNo, CancellationToken cancellationToken = default);
    }
}
