using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.InputModel
{
    public enum EInvoiceDocumentType
    {
        Invoice = 01,
        CreditNote = 02,
        DebitNote = 03,
        RefundNote = 04,
        SelfBilledInvoice = 11,
        SelfBilledCreditNote = 12,
        SelfBilledDebitNote = 13,
        SelfBilledRefundNote = 14,
    }

    public static class DocumentTypeDisplay
    {
        public static Dictionary<EInvoiceDocumentType, string> GetDisplayNames()
        {
            return new Dictionary<EInvoiceDocumentType, string>
            {
                { EInvoiceDocumentType.Invoice, "Invoice" },
                { EInvoiceDocumentType.CreditNote, "Credit Note" },
                { EInvoiceDocumentType.DebitNote, "Debit Note" },
                { EInvoiceDocumentType.RefundNote, "Refund Note" },
                { EInvoiceDocumentType.SelfBilledInvoice, "Self-billed Invoice" },
                { EInvoiceDocumentType.SelfBilledCreditNote, "Self-billed Credit Note" },
                { EInvoiceDocumentType.SelfBilledDebitNote, "Self-billed Debit Note" },
                { EInvoiceDocumentType.SelfBilledRefundNote, "Self-billed Refund Note" }
            };
        }
    }
}
