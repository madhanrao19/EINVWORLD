using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.ViewModels;
using eInvWorld.Services;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Template lines used to drop a line's Fee / Charge on save, and Edit's Update / Load template also dropped
    /// the discount. Every save and load now goes through <see cref="InvoiceTemplateService"/>.
    /// </summary>
    public class InvoiceTemplateLineTests
    {
        private static InvoiceLineView Line() => new()
        {
            ClassificationCode = "022", ItemDescription = "Service", UnitOfMeasure = "C62",
            Quantity = 2, UnitPrice = 100m, DiscountAmount = 20m, FeeChargeAmount = 15m, FeeChargeReason = "Delivery",
            Taxes = new List<InvoiceTaxView> { new() { TaxCategory = "02", TaxPercentage = 8m } }
        };

        [Fact]
        public void Save_Keeps_Discount_And_Fee_And_Calculates_Like_An_Invoice_Line()
        {
            var saved = new InvoiceTemplateService().MapToTemplateLines(new List<InvoiceLineView> { Line() }).Single();

            Assert.Equal(20m, saved.DiscountAmount);
            Assert.Equal(15m, saved.FeeChargeAmount);
            Assert.Equal("Delivery", saved.FeeChargeReason);
            Assert.Equal(195.00m, saved.AmountExclTax);   // 200 - 20 + 15
            Assert.Equal(15.60m, saved.Taxes.Single().TaxAmount);
            Assert.Equal(210.60m, saved.AmountInclTax);
        }

        [Fact]
        public void Load_Round_Trips_Discount_Fee_And_Taxes()
        {
            var service = new InvoiceTemplateService();
            var loaded = service.ToLineViews(service.MapToTemplateLines(new List<InvoiceLineView> { Line(), Line() }));

            Assert.Equal(new[] { 1, 2 }, loaded.Select(l => l.LineNumber));
            var first = loaded[0];
            Assert.Equal(20m, first.DiscountAmount);
            Assert.Equal(15m, first.FeeChargeAmount);
            Assert.Equal("Delivery", first.FeeChargeReason);
            Assert.Equal(195.00m, first.AmountExclTax);
            Assert.Equal("02", first.Taxes.Single().TaxCategory);
            Assert.Equal(8m, first.Taxes.Single().TaxPercentage);
        }

        [Fact]
        public void Load_Of_No_Lines_Is_Empty() => Assert.Empty(new InvoiceTemplateService().ToLineViews(null));
    }
}
