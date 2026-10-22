using eInvWorld.Helpers;
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
