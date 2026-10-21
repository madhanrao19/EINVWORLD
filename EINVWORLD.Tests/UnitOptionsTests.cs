using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>Unit dropdown: Finance-approved list only, saved codes outside it kept selectable.</summary>
    public class UnitOptionsTests
    {
        // Active LHDN units: the 11 Finance codes plus two others a saved item could use.
        private static Dictionary<string, string> Active(params string[] without)
        {
            var d = DropdownHelper.FinanceUnits.ToDictionary(u => u.Code, u => u.Code.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
            d["C62"] = "one";
            d["KGM"] = "kilogram";
            foreach (var c in without) d.Remove(c);
            return d;
        }

        [Fact]
        public void Lists_only_the_finance_units_in_order_with_friendly_names()
        {
            var options = DropdownHelper.BuildUnitOptions(Active(), null);

            Assert.Equal(new[] { "EA", "XUN", "H87", "E48", "HUR", "DAY", "E51", "LS", "SET", "ZP", "XST" }, options.Select(o => o.Value));
            Assert.Equal(new[] { "Each", "Unit", "Piece (pcs)", "Service", "Hour", "Day", "Job / Project", "Lump Sum (LS)", "Set", "Page", "Sheet" },
                options.Select(o => o.Text));
            Assert.All(options, o => Assert.Null(o.Group)); // no "Common Units" header
            Assert.DoesNotContain(options, o => o.Value == "KGM");
        }

        [Fact]
        public void Skips_a_finance_unit_that_is_inactive()
        {
            var options = DropdownHelper.BuildUnitOptions(Active("ZP"), null);

            Assert.Equal(10, options.Count);
            Assert.DoesNotContain(options, o => o.Value == "ZP");
        }

        [Fact]
        public void Keeps_a_saved_code_outside_the_list_once_and_ignores_unknown_or_blank()
        {
            var options = DropdownHelper.BuildUnitOptions(Active(), new[] { "C62", "c62", "EA", "NOPE", "", null });

            Assert.Equal(12, options.Count);
            Assert.Equal("one (C62)", options.Last().Text);
            Assert.Single(options, o => o.Value == "EA");
        }
    }
}
