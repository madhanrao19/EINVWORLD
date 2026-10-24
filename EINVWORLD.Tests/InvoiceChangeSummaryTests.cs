using System;
using System.Collections.Generic;
using System.Linq;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>The "Edited" activity entry: what changed between the saved draft and the new save.</summary>
    public class InvoiceChangeSummaryTests
    {
        private static LineSnapshot Line(string desc = "Service", string tax = "01", decimal rate = 8m, decimal qty = 1, decimal price = 100m) =>
            new(desc, "022", "C62", qty, price, 0m, 0m, tax, rate);

        private static InvoiceSnapshot Inv(string buyer = "ABC Sdn Bhd", decimal total = 108m, params LineSnapshot[] lines) =>
            new(buyer, "01", "MYR", new DateTime(2026, 10, 24), total, lines.Length == 0 ? new List<LineSnapshot> { Line() } : lines.ToList());

        [Fact]
        public void Nothing_Changed_Is_Null() => Assert.Null(InvoiceChangeSummary.Describe(Inv(), Inv()));

        [Fact]
        public void Header_And_Line_Changes_Read_Like_Finance_Would_Say_Them()
        {
            var text = InvoiceChangeSummary.Describe(
                Inv("ABC Sdn Bhd", 108m, Line()),
                Inv("XYZ Bhd", 100m, Line(tax: "06", rate: 0m)));
            Assert.Equal("Buyer: ABC Sdn Bhd → XYZ Bhd; Item 1 Tax Type: 01 → 06; Item 1 Tax Rate: 8 → 0; Total: 108.00 → 100.00", text);
        }

        [Fact]
        public void Quantity_Price_Discount_And_Fee_Are_Reported()
        {
            var after = Line(qty: 2, price: 90m) with { Discount = 5m, Fee = 10m, Classification = "008", Unit = "EA", Description = "Consulting" };
            var text = InvoiceChangeSummary.Describe(Inv(lines: Line()), Inv(lines: after))!;
            foreach (var part in new[] { "Item 1 Description: Service → Consulting", "Item 1 Classification: 022 → 008", "Item 1 Unit: C62 → EA",
                                         "Item 1 Quantity: 1 → 2", "Item 1 Unit Price: 100.00 → 90.00", "Item 1 Discount: 0.00 → 5.00", "Item 1 Fee/Charge: 0.00 → 10.00" })
                Assert.Contains(part, text);
        }

        [Fact]
        public void Added_And_Removed_Items()
        {
            Assert.Contains("Item 2 added: Delivery", InvoiceChangeSummary.Describe(Inv(lines: Line()), Inv(lines: new[] { Line(), Line("Delivery") }))!);
            Assert.Contains("Item 2 removed: Delivery", InvoiceChangeSummary.Describe(Inv(lines: new[] { Line(), Line("Delivery") }), Inv(lines: Line()))!);
        }

        [Fact]
        public void Blank_And_Missing_Count_As_The_Same()
        {
            var a = Inv() with { Currency = null };
            var b = Inv() with { Currency = " " };
            Assert.Null(InvoiceChangeSummary.Describe(a, b));
            Assert.Equal("Currency: (blank) → MYR", InvoiceChangeSummary.Describe(a, Inv()));
        }

        [Fact]
        public void Long_Summaries_Fit_The_Remarks_Column()
        {
            var many = Enumerable.Range(1, 60).Select(i => Line($"Item number {i} with a long description")).ToArray();
            var text = InvoiceChangeSummary.Describe(Inv(lines: Line()), Inv(lines: many))!;
            Assert.Equal(InvoiceChangeSummary.MaxLength, text.Length);
            Assert.EndsWith("…", text);
        }
    }
}
