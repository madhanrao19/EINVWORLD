
using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.JsonModels
{
    public class Invoice
    {
        public List<ID> ID { get; set; } = new();
        public List<IssueDate> IssueDate { get; set; } = new();
        public List<IssueTime> IssueTime { get; set; } = new();
        public List<InvoiceTypeCode> InvoiceTypeCode { get; set; } = new();
        public List<DocumentCurrencyCode> DocumentCurrencyCode { get; set; } = new();
        public List<InvoicePeriod> InvoicePeriod { get; set; } = new();
        public List<BillingReference> BillingReference { get; set; } = new();
        public List<AdditionalDocumentReference> AdditionalDocumentReference { get; set; } = new();
        public List<AccountingSupplierParty> AccountingSupplierParty { get; set; } = new();
        public List<AccountingCustomerParty> AccountingCustomerParty { get; set; } = new();
        public List<Delivery> Delivery { get; set; } = new();
        public List<PaymentMeans> PaymentMeans { get; set; } = new();
        public List<PaymentTerms> PaymentTerms { get; set; } = new();
        public List<PrepaidPayment> PrepaidPayment { get; set; } = new();
        public List<AllowanceCharge> AllowanceCharge { get; set; } = new();
        public List<TaxTotal> TaxTotal { get; set; } = new();
        public List<LegalMonetaryTotal> LegalMonetaryTotal { get; set; } = new();
        public List<InvoiceLine> InvoiceLine { get; set; } = new();
        public List<TaxExchangeRate> TaxExchangeRate { get; set; } = new();

    }
}
