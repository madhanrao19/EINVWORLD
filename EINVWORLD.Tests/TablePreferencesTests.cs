using eInvWorld.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EINVWORLD.Tests
{
    public class TablePreferencesTests
    {
        [Theory]
        [InlineData("items")]
        [InlineData("code-taxtypes")]
        [InlineData("ai-capture-history")]
        [InlineData("invoices:received")]
        public void IsValidKey_AcceptsTableKeys(string key) => Assert.True(TablePreferences.IsValidKey(key));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Items")]           // lower-case only
        [InlineData("../users")]
        [InlineData("a b")]
        [InlineData("-items")]
        [InlineData("x\" onload=1")]
        public void IsValidKey_RejectsAnythingElse(string? key) => Assert.False(TablePreferences.IsValidKey(key));

        [Fact]
        public void IsValidKey_RejectsOver64Chars() => Assert.False(TablePreferences.IsValidKey(new string('a', 65)));

        [Fact]
        public void Normalize_TrimsAndDeduplicates()
        {
            var p = TablePreferences.Normalize(new TableColumnPreference { Hidden = new() { " UUID ", "UUID" }, Order = new() { "Total", "Status", "Total" } });
            Assert.NotNull(p);
            Assert.Equal(new[] { "UUID" }, p!.Hidden);
            Assert.Equal(new[] { "Total", "Status" }, p.Order);
        }

        [Fact]
        public void Normalize_RejectsOversizedInput()
        {
            Assert.Null(TablePreferences.Normalize(new TableColumnPreference { Order = Enumerable.Range(0, 61).Select(i => "c" + i).ToList() }));
            Assert.Null(TablePreferences.Normalize(new TableColumnPreference { Hidden = new() { new string('x', 121) } }));
            Assert.Null(TablePreferences.Normalize(new TableColumnPreference { Hidden = new() { "  " } }));
            Assert.Null(TablePreferences.Normalize(null));
        }

        [Fact]
        public void Write_KeepsOtherPreferences_IncludingInvoiceListKeys()
        {
            var existing = "{\"invoiceListColumns\":{\"col-uuid\":false},\"invoiceListColumnOrder\":[\"col-invoice-no\"],\"theme\":\"x\"}";
            var json = TablePreferences.Write(existing, "items", new TableColumnPreference { Hidden = new() { "UUID" }, Order = new() { "Name", "Code" } });
            var root = JObject.Parse(json!);
            Assert.False((bool)root["invoiceListColumns"]!["col-uuid"]!);
            Assert.Equal("col-invoice-no", (string)root["invoiceListColumnOrder"]![0]!);
            Assert.Equal("x", (string)root["theme"]!);
            Assert.Equal("UUID", (string)root["tableColumns:items"]!["hidden"]![0]!);
        }

        [Fact]
        public void WriteThenRead_RoundTrips()
        {
            var json = TablePreferences.Write(null, "items", new TableColumnPreference { Hidden = new() { "UUID" }, Order = new() { "Total", "Name" } });
            var read = TablePreferences.Read(json, new[] { "items", "missing", "../bad" });
            Assert.Single(read);
            Assert.Equal(new[] { "UUID" }, read["items"].Hidden);
            Assert.Equal(new[] { "Total", "Name" }, read["items"].Order);
        }

        [Fact]
        public void Write_EmptyPreference_RemovesTheEntry()
        {
            var json = TablePreferences.Write(null, "items", new TableColumnPreference { Hidden = new() { "UUID" } });
            json = TablePreferences.Write(json, "items", new TableColumnPreference());
            Assert.Null(JObject.Parse(json!)["tableColumns:items"]);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("[1,2]")]
        public void Write_ReplacesUnreadableJson(string existing)
        {
            var json = TablePreferences.Write(existing, "items", new TableColumnPreference { Hidden = new() { "UUID" } });
            Assert.NotNull(JObject.Parse(json!)["tableColumns:items"]);
            Assert.Empty(TablePreferences.Read(existing, new[] { "items" }));
        }

        [Fact]
        public void Write_CapsTheNumberOfSavedTables_ButStillUpdatesExistingOnes()
        {
            string? json = null;
            for (var i = 0; i < TablePreferences.MaxTablesPerUser; i++)
                json = TablePreferences.Write(json, "t" + i, new TableColumnPreference { Hidden = new() { "A" } });
            Assert.Null(TablePreferences.Write(json, "one-more", new TableColumnPreference { Hidden = new() { "A" } }));
            Assert.NotNull(TablePreferences.Write(json, "t0", new TableColumnPreference { Hidden = new() { "B" } }));
        }

        [Fact]
        public void Write_RejectsWhenTheWholeBlobWouldBeTooLarge()
        {
            var big = "{\"other\":\"" + new string('x', TablePreferences.MaxTotalJsonLength) + "\"}";
            Assert.Null(TablePreferences.Write(big, "items", new TableColumnPreference { Hidden = new() { "A" } }));
        }

        [Fact]
        public void Write_KeepsOtherValuesExactly_IncludingDateLikeStrings()
        {
            var existing = "{\"savedAt\":\"2026-10-14T10:00:00\",\"n\":1.50}";
            var json = TablePreferences.Write(existing, "items", new TableColumnPreference { Hidden = new() { "A" } });
            Assert.Contains("\"savedAt\":\"2026-10-14T10:00:00\"", json);
        }

        [Fact]
        public void Read_IgnoresDamagedEntries()
        {
            var json = "{\"tableColumns:items\":{\"hidden\":\"not-a-list\"},\"tableColumns:ok\":{\"hidden\":[\"A\"],\"order\":[]}}";
            var read = TablePreferences.Read(json, new[] { "items", "ok" });
            Assert.False(read.ContainsKey("items"));
            Assert.Equal(new[] { "A" }, read["ok"].Hidden);
        }
    }
}
