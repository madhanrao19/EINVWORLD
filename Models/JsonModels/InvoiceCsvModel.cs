using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models.JsonModels
{
    public class InvoiceCsvModel
    {
        public string InvoiceNumber { get; set; } = null!;
        public string IssueDate { get; set; } = null!;
        public string IssueTime { get; set; } = null!;
        public string InvoiceTypeCode { get; set; } = null!;
        public string CurrencyCode { get; set; } = null!;
        public string StartDate { get; set; } = null!;
        public string EndDate { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string BillReferenceNumber { get; set; } = null!;
        public string OriginInvoiceUUID { get; set; } = null!;
        public string AdditionalAccountID { get; set; } = null!;
        public string schemeAgencyName { get; set; } = null!;
        public string IndustryClassificationCode { get; set; } = null!;
        public string BusinessActivityDescription { get; set; } = null!;
        public string SupplierTIN { get; set; } = null!;
        public string SupplierBRN { get; set; } = null!;
        public string SupplierSST { get; set; } = null!;
        public string SupplierTTX { get; set; } = null!;
        public string SupplierCityName { get; set; } = null!;
        public string SupplierPostalZone { get; set; } = null!;
        public string SupplierCountryCode { get; set; } = null!;
        public string SupplierAddressLine1 { get; set; } = null!;
        public string SupplierAddressLine2 { get; set; } = null!;
        public string SupplierAddressLine3 { get; set; } = null!;
        public string SupplierRegistrationName { get; set; } = null!;
        public string SupplierContactNo { get; set; } = null!;
        public string SupplierEmail { get; set; } = null!;

        public string BuyerTIN { get; set; } = null!;
        public string BuyerBRN { get; set; } = null!;
        public string BuyerSST { get; set; } = null!;
        public string BuyerTTX { get; set; } = null!;
        public string BuyerCityName { get; set; } = null!;
        public string BuyerPostalZone { get; set; } = null!;
        public string BuyerCountryCode { get; set; } = null!;
        public string BuyerAddressLine1 { get; set; } = null!;
        public string BuyerAddressLine2 { get; set; } = null!;
        public string BuyerAddressLine3 { get; set; } = null!;
        public string BuyerRegistrationName { get; set; } = null!;
        public string BuyerContactNo { get; set; } = null!;
        public string BuyerEmail { get; set; } = null!;

        public string ShippingRecipientName { get; set; } = null!;
        public string ShippingRecipientCityName { get; set; } = null!;
        public string ShippingRecipientPostalZone { get; set; } = null!;
        public string ShippingRecipientCountryCode { get; set; } = null!;
        public string ShippingRecipientAddressLine1 { get; set; } = null!;
        public string ShippingRecipientAddressLine2 { get; set; } = null!;
        public string ShippingRecipientAddressLine3 { get; set; } = null!;
        public string ShippingRecipientIDNo { get; set; } = null!;
        public string ShippingRecipientIDType { get; set; } = null!;
        public string ShippingDescription { get; set; } = null!;
        public string ShippingServiceCharge { get; set; } = null!;

        public string PaymentCode { get; set; } = null!;
        public string SupplierBankAccountNo { get; set; } = null!;
        public string PaymentTerms { get; set; } = null!;

        [StringLength(150)]
        public string? PrepaymentReferenceNumber { get; set; }
        public string PrePaymentAmount { get; set; } = null!;
        public string PrePaymentDate { get; set; } = null!;
        public string PrePaymentTime { get; set; } = null!;
        public string PrePaymentTime11 { get; set; } = null!;  //

        public string DiscountDescription { get; set; } = null!;
        public string TotalDiscountAmount { get; set; } = null!;
        public string TaxAmount { get; set; } = null!;
        public string TaxableAmount { get; set; } = null!;
        public string TaxType { get; set; } = null!;

        public string LineExtensionAmount { get; set; } = null!;
        public string TaxExclusiveAmount { get; set; } = null!;
        public string TaxInclusiveAmount { get; set; } = null!;
        public string AllowanceTotalAmount { get; set; } = null!;
        public string ChargeTotalAmount { get; set; } = null!;
        public string PayableAmount { get; set; } = null!;
        public string PayableRoundingAmount { get; set; } = null!;

        public string DiscountAmount { get; set; } = null!;
        public string DiscountRate { get; set; } = null!;
        //public string DiscountDescription { get; set; }

        public string Line { get; set; } = null!;
        public string Quantity { get; set; } = null!;
        public string Measurement { get; set; } = null!;
        public string PTC { get; set; } = null!;
        public string ItemClassificationCode { get; set; } = null!;
        public string ItemDescription { get; set; } = null!;
        public string CountryCode { get; set; } = null!;

        public string Subtotal { get; set; } = null!;
        public string TotalExcludingTax { get; set; } = null!;
        public string UnitPrice { get; set; } = null!;
        //public string TaxAmount { get; set; }


    }
}
