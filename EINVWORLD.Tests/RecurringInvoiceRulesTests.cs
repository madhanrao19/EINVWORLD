using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.InputModel;
using eInvWorld.Services.Background;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Recurring generation used to copy the template's billing period forever, copy its stored header totals
    /// (which could disagree with the recalculated lines) and skip the Tax Type 06 / default-classification rules.
    /// </summary>
    public class RecurringInvoiceRulesTests
    {
        [Theory]
        [InlineData("Monthly", "2026-11-01", "2026-11-01", "2026-11-30")]
        [InlineData("Monthly", "2028-02-15", "2028-02-01", "2028-02-29")] // leap year
        [InlineData("Weekly", "2026-10-25", "2026-10-19", "2026-10-25")]  // Sunday belongs to the week starting Monday
        [InlineData("Weekly", "2026-10-19", "2026-10-19", "2026-10-25")]
        [InlineData("Daily", "2026-10-24", "2026-10-24", "2026-10-24")]
        [InlineData("Annually", "2026-06-30", "2026-01-01", "2026-12-31")]
        [InlineData("Unknown", "2026-12-31", "2026-12-01", "2026-12-31")]  // treated as Monthly
        public void BillingPeriodFor_Is_The_Calendar_Period_The_Run_Falls_In(string frequency, string run, string start, string end)
        {
            var (s, e) = RecurringInvoiceWorker.BillingPeriodFor(DateTime.Parse(run).AddHours(9.5), frequency);
            Assert.Equal(DateTime.Parse(start), s);
            Assert.Equal(DateTime.Parse(end), e);
        }

        private static InvoiceHeader Invoice(string docType, params InvoiceLine[] lines) =>
            new() { DocTypeCode = docType, InvoiceLines = new List<InvoiceLine>(lines) };

        private static InvoiceLine Line(decimal qty, decimal price, decimal discount, string? classification, string taxType, decimal rate) =>
            new()
            {
                Quantity = qty, UnitPrice = price, DiscountAmount = discount, ClassificationCode = classification!,
                InvoiceTaxes = new List<InvoiceTax> { new() { TaxCategory = taxType, TaxPercentage = rate, TaxAmount = 999m } }
            };

        [Fact]
        public void ApplyLineRulesAndTotals_Recomputes_Totals_From_The_Lines_And_Zeroes_Tax_Type_06()
        {
            var inv = Invoice("01",
                Line(2, 100m, 20m, null, "02", 8m),      // 180.00 + 14.40 tax
                Line(1, 50m, 0m, "", "06", 8m));         // 06 with a rate saved before the rule existed
            inv.TotalAmountIncTax = 12345m;               // stale template totals must be replaced

            RecurringInvoiceWorker.ApplyLineRulesAndTotals(inv, "C1234567890");

            Assert.All(inv.InvoiceLines, l => Assert.Equal("022", l.ClassificationCode));
            Assert.Equal(0m, inv.InvoiceLines.ElementAt(1).InvoiceTaxes[0].TaxPercentage);
            Assert.Equal(0m, inv.InvoiceLines.ElementAt(1).InvoiceTaxes[0].TaxAmount);
            Assert.Equal(230.00m, inv.TotalAmountExclTax);
            Assert.Equal(14.40m, inv.TotalTaxAmount);
            Assert.Equal(244.40m, inv.TotalAmountIncTax);
            Assert.Equal(20m, inv.TotalDiscountAmount);
            Assert.Equal(244.40m, inv.TotalPayableAmount);
            Assert.Equal(244.40m, inv.TotalNetAmount);
        }

        [Fact]
        public void ApplyLineRulesAndTotals_Adds_A_Template_Fee_To_The_Taxable_Base_And_Totals()
        {
            var line = Line(2, 100m, 20m, "022", "02", 8m);
            line.FeeChargeAmount = 15m;                   // carried over from the template line
            var inv = Invoice("01", line);

            RecurringInvoiceWorker.ApplyLineRulesAndTotals(inv, "C1234567890");

            Assert.Equal(195.00m, inv.TotalAmountExclTax);
            Assert.Equal(15.60m, inv.TotalTaxAmount);
            Assert.Equal(210.60m, inv.TotalPayableAmount);
        }

        [Fact]
        public void ApplyLineRulesAndTotals_Keeps_A_Chosen_Classification()
        {
            var inv = Invoice("01", Line(1, 10m, 0m, "008", "01", 10m));
            RecurringInvoiceWorker.ApplyLineRulesAndTotals(inv, null);
            Assert.Equal("008", inv.InvoiceLines.First().ClassificationCode);
            Assert.Equal(1m, inv.TotalTaxAmount);
        }

        [Theory]
        [InlineData("01", "C1234567890", "022")]
        [InlineData("11", "C1234567890", "004")]
        [InlineData("11", "EI00000000010", "004")]
        [InlineData("12", "EI00000000030", "035")]
        [InlineData("02", "EI00000000030", "022")]
        public void DefaultClassificationCode_Follows_The_Document_Type_And_Supplier(string docType, string supplierTin, string expected) =>
            Assert.Equal(expected, TinHelper.DefaultClassificationCode(docType, supplierTin));
    }
}
