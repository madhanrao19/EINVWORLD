using eInvWorld.Helpers;
using eInvWorld.Models.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Tax Type 06 ("Not Applicable") must be 0% / 0.00 (LHDN CF394). Other tax types — including a genuine
    /// 0% rate and Tax Exemption (E) — are not affected by this rule.
    /// </summary>
    public class TaxTypeRulesTests
    {
        [Theory]
        [InlineData("06", 0, 0, false)]        // 06 + 0% -> valid
        [InlineData("06", null, null, false)]  // 06 with nothing entered -> valid
        [InlineData("06", 8, 0, true)]         // legacy 06 + 8% -> blocked
        [InlineData("06", 0, 5, true)]         // 06 + non-zero tax amount -> blocked
        [InlineData(" 06 ", 8, 0, true)]       // stray whitespace doesn't slip through
        [InlineData("02", 8, 0, false)]        // Service Tax 8% -> valid
        [InlineData("01", 0, 0, false)]        // Sales Tax 0% -> valid
        [InlineData("E", 0, 0, false)]         // Tax Exemption stays separate
        [InlineData("E", 6, 3, false)]         // ...this rule never forces E to zero
        public void NotApplicable_MustBeZeroRateAndAmount(string category, int? pct, int? amount, bool invalid) =>
            Assert.Equal(invalid, TaxTypeRules.IsInvalidNotApplicable(category, (decimal?)pct, (decimal?)amount));

        [Fact]
        public void ZeroNotApplicable_ForcesOnly06ToZero_AndLeavesOtherTypesAlone()
        {
            var lines = new List<InvoiceLineView>
            {
                new() { Taxes = new() { new() { TaxCategory = "06", TaxPercentage = 8m, TaxAmount = 8m } } },   // tampered POST
                new() { Taxes = new() { new() { TaxCategory = "06", TaxPercentage = 0m, TaxAmount = 0m } } },   // already right
                new() { Taxes = new() { new() { TaxCategory = "02", TaxPercentage = 8m, TaxAmount = 8m } } },   // Service Tax keeps its rate
                new() { Taxes = new() { new() { TaxCategory = "E", TaxPercentage = 6m, TaxAmount = 6m, TaxExemptionReason = "x" } } },
                new() { Taxes = null! },                                                                            // no taxes bound
            };

            Assert.Equal(1, TaxTypeRules.ZeroNotApplicable(lines));
            Assert.Equal((0m, 0m), (lines[0].Taxes[0].TaxPercentage!.Value, lines[0].Taxes[0].TaxAmount!.Value));
            Assert.Equal(0m, lines[1].Taxes[0].TaxPercentage);
            Assert.Equal((8m, 8m), (lines[2].Taxes[0].TaxPercentage!.Value, lines[2].Taxes[0].TaxAmount!.Value));
            Assert.Equal(6m, lines[3].Taxes[0].TaxPercentage);
            Assert.Equal(0, TaxTypeRules.ZeroNotApplicable(null));
        }

        [Fact]
        public void ZeroNotApplicable_ThenCalculate_GivesNoTaxOn06()
        {
            var line = new InvoiceLineView { Quantity = 2, UnitPrice = 50m, ItemDescription = "Laptop", UnitOfMeasure = "EA", ClassificationCode = "003",
                Taxes = new() { new() { TaxCategory = "06", TaxPercentage = 10m } } };

            TaxTypeRules.ZeroNotApplicable(new[] { line });
            line.CalculateAmounts();

            Assert.Equal(0m, line.Taxes[0].TaxAmount);
            Assert.Equal(100m, line.AmountInclTax);
            Assert.Equal("003", line.ClassificationCode); // the rule never touches the classification
        }

        [Fact]
        public void Message_NamesTheLine_InFinanceTerms() =>
            Assert.Equal("Item 2 – Standby Man Days (Miri): Tax Rate must be 0% when Tax Type is Not Applicable.",
                TaxTypeRules.Message("2", "Standby Man Days (Miri)"));

        [Fact]
        public void PayloadErrors_FindsTheBadLine_InAStoredUblFile()
        {
            // Shape the mapper writes: [{ "_": value }] wrappers, line IDs "001".
            const string json = @"{""Invoice"":[{""InvoiceLine"":[
              {""ID"":[{""_"":""001""}],""Item"":[{""Description"":[{""_"":""Rig hire""}]}],
               ""TaxTotal"":[{""TaxSubtotal"":[{""TaxAmount"":[{""_"":8.0}],""Percent"":[{""_"":8.0}],""TaxCategory"":[{""ID"":[{""_"":""02""}]}]}]}]},
              {""ID"":[{""_"":""002""}],""Item"":[{""Description"":[{""_"":""Standby Man Days (Miri)""}]}],
               ""TaxTotal"":[{""TaxSubtotal"":[{""TaxAmount"":[{""_"":16.0}],""Percent"":[{""_"":8.0}],""TaxCategory"":[{""ID"":[{""_"":""06""}]}]}]}]}
            ]}]}";

            var errors = TaxTypeRules.PayloadErrors(json);

            Assert.Equal(new[] { TaxTypeRules.Message("2", "Standby Man Days (Miri)") }, errors);
        }
    }
}
