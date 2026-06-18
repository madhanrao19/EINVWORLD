// File: Models/ViewModels/PdfTemplate_v2ViewModel.cs
using eInvWorld.Models.InputModel;

namespace EINVWORLD.Models.ViewModels
{
    public class PdfTemplate_v2ViewModel
    {
        public InvoiceHeader? InvoiceDetail { get; set; }
        public List<InvoiceLine> InvoiceLines { get; set; } = new();
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalAmountInclTax { get; set; }
        public string? FullCurrencyName { get; set; }
        public string? QRCodeBase64 { get; set; }
        public string? InvoiceTypeDescription { get; set; }
    }
}
