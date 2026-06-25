using System.Collections.Generic;
using eInvWorld.Models.ViewModels;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pure money-math tests for invoice line and header totals. These guard the core financial
    /// correctness: line = Qty*UnitPrice, ExclTax = line - discount, tax = rate% * ExclTax,
    /// InclTax = ExclTax + ΣTax, and header totals = sum over lines.
    /// </summary>
    public class InvoiceCalculationTests
    {
        private static InvoiceLineView MakeLine(decimal? qty, decimal? price, decimal? discount, params decimal?[] taxPercents)
        {
            var line = new InvoiceLineView
            {
                ItemDescription = "Item",
                UnitOfMeasure = "C62",
                ClassificationCode = "022",
                Quantity = qty,
                UnitPrice = price,
                DiscountAmount = discount,
            };
            foreach (var p in taxPercents)
                line.Taxes.Add(new InvoiceTaxView { TaxCategory = "01", TaxPercentage = p });
            return line;
        }

        // ── InvoiceLineView.CalculateAmounts ──────────────────────────────────────────────────
        [Fact]
        public void Line_BasicWithTax()
        {
            var line = MakeLine(qty: 3, price: 100, discount: null, 10m);

            line.CalculateAmounts();

            Assert.Equal(300m, line.Subtotal);
            Assert.Equal(0m, line.DiscountAmount); // null discount is normalised to 0
            Assert.Equal(300m, line.AmountExclTax);
            Assert.Equal(30m, line.Taxes[0].TaxAmount);
            Assert.Equal(330m, line.AmountInclTax);
        }

        [Fact]
        public void Line_WithDiscount_ReducesExclTaxBase()
        {
            var line = MakeLine(qty: 2, price: 50, discount: 10, 8m);

            line.CalculateAmounts();

            Assert.Equal(100m, line.Subtotal);
            Assert.Equal(90m, line.AmountExclTax);      // 100 - 10
            Assert.Equal(7.2m, line.Taxes[0].TaxAmount); // 8% of 90
            Assert.Equal(97.2m, line.AmountInclTax);
        }

        [Fact]
        public void Line_NegativeDiscount_ActsAsCharge()
        {
            var line = MakeLine(qty: 1, price: 100, discount: -10, 0m);

            line.CalculateAmounts();

            Assert.Equal(110m, line.AmountExclTax); // 100 - (-10)
            Assert.Equal(110m, line.AmountInclTax);
        }

        [Fact]
        public void Line_ZeroQuantity_AllZero()
        {
            var line = MakeLine(qty: 0, price: 100, discount: null, 10m);

            line.CalculateAmounts();

            Assert.Equal(0m, line.Subtotal);
            Assert.Equal(0m, line.AmountExclTax);
            Assert.Equal(0m, line.AmountInclTax);
        }

        [Fact]
        public void Line_NullQuantityOrPrice_NoCalculation()
        {
            var line = MakeLine(qty: null, price: 100, discount: null, 10m);

            line.CalculateAmounts();

            Assert.Null(line.Subtotal);
            Assert.Null(line.AmountExclTax);
            Assert.Null(line.AmountInclTax);
        }

        [Fact]
        public void Line_MultipleTaxes_SumIntoInclTax()
        {
            var line = MakeLine(qty: 1, price: 100, discount: null, 6m, 10m);

            line.CalculateAmounts();

            Assert.Equal(100m, line.AmountExclTax);
            Assert.Equal(6m, line.Taxes[0].TaxAmount);
            Assert.Equal(10m, line.Taxes[1].TaxAmount);
            Assert.Equal(116m, line.AmountInclTax); // 100 + 6 + 10
        }

        [Fact]
        public void Line_TaxExemptZeroPercent_NoTaxAdded()
        {
            var line = MakeLine(qty: 4, price: 25, discount: null, 0m);

            line.CalculateAmounts();

            Assert.Equal(100m, line.AmountExclTax);
            Assert.Equal(0m, line.Taxes[0].TaxAmount);
            Assert.Equal(100m, line.AmountInclTax);
        }

        // ── InvoiceHeaderView.CalculateInvoiceTotals ──────────────────────────────────────────
        [Fact]
        public void Header_NoLines_AllZero()
        {
            var header = new InvoiceHeaderView { DocTypeCode = "01", Currency = "MYR", ForeignCurrency = "", InvoiceLines = new List<InvoiceLineView>() };

            header.CalculateInvoiceTotals();

            Assert.Equal(0m, header.TotalAmountExclTax);
            Assert.Equal(0m, header.TotalTaxAmount);
            Assert.Equal(0m, header.TotalAmountIncTax);
            Assert.Equal(0m, header.TotalPayableAmount);
        }

        [Fact]
        public void Header_MultiLine_TotalsAreSums()
        {
            var header = new InvoiceHeaderView
            {
                DocTypeCode = "01",
                Currency = "MYR",
                ForeignCurrency = "",
                InvoiceLines = new List<InvoiceLineView>
                {
                    MakeLine(qty: 2, price: 100, discount: null, 10m), // excl 200, tax 20, incl 220
                    MakeLine(qty: 1, price: 50, discount: 5, 6m),      // excl 45, tax 2.7, incl 47.7
                }
            };

            header.CalculateInvoiceTotals();

            Assert.Equal(245m, header.TotalAmountExclTax);   // 200 + 45
            Assert.Equal(22.7m, header.TotalTaxAmount);      // 20 + 2.7
            Assert.Equal(267.7m, header.TotalAmountIncTax);  // 220 + 47.7
            Assert.Equal(5m, header.TotalDiscountAmount);
            Assert.Equal(header.TotalAmountIncTax, header.TotalPayableAmount);
            Assert.Equal(header.TotalAmountIncTax, header.TotalNetAmount);
        }
    }
}
