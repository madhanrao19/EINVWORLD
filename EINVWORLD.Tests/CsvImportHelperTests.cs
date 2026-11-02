using System;
using System.IO;
using System.Linq;
using System.Text;
using EINVWORLD.Helpers;
using Xunit;
using InvoiceRow = EINVWORLD.Pages.Invoices.ImportCSVModel.InvoiceCsvDto;
using ItemRow = eInvWorld.Pages.Items.ImportModel.ItemCsvDto;
using BuyerRow = eInvWorld.Pages.PublicCustomer.ImportModel.PublicCustomerCsvDto;
using CompanyRow = eInvWorld.Pages.Suppliers.ImportModel.PartyInfoCsvDto;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pins how the CSV imports read files (written against CsvHelper 33, kept green across the
    /// CsvHelper 34 migration) so a library upgrade can't silently change what gets imported.
    /// </summary>
    public class CsvImportHelperTests
    {
        private static Stream Csv(string text, bool bom = false)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bom) bytes = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
            return new MemoryStream(bytes);
        }

        // ── ReadRecords<T> (Items, Buyers, Companies, Invoice CSV import) ──────────────────────────

        [Fact]
        public void ReadRecords_MapsByHeader_IgnoresExtraColumns_MissingColumnsStayNull()
        {
            var rows = CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,IssueDate,SomethingElse,Quantity,UnitPrice\r\n" +
                "INV-1,2026-11-02,ignored,2,10.50\r\n"));

            var r = Assert.Single(rows);
            Assert.Equal("INV-1", r.InvoiceNo);
            Assert.Equal("2026-11-02", r.IssueDate);
            Assert.Equal(2m, r.Quantity);
            Assert.Equal(10.50m, r.UnitPrice);
            Assert.Null(r.CounterpartyTIN);   // column not in the file
            Assert.Null(r.TaxPercentage);     // column not in the file
        }

        [Fact]
        public void ReadRecords_TrimsValues()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,ItemDescription,Quantity\n  INV-2  ,  Widget  ,  3  \n")));

            Assert.Equal("INV-2", r.InvoiceNo);
            Assert.Equal("Widget", r.ItemDescription);
            Assert.Equal(3m, r.Quantity);
        }

        [Fact]
        public void ReadRecords_EmptyCell_StringIsEmpty_NullableNumberIsNull()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,PoDoNo,Quantity,DiscountAmount\nINV-3,,,\n")));

            Assert.Equal("", r.PoDoNo);
            Assert.Null(r.Quantity);
            Assert.Null(r.DiscountAmount);
        }

        [Fact]
        public void ReadRecords_InvariantDecimals_AndNegatives()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,UnitPrice,DiscountAmount,TaxPercentage\nINV-4,1234.5678,-5,8\n")));

            Assert.Equal(1234.5678m, r.UnitPrice);
            Assert.Equal(-5m, r.DiscountAmount);
            Assert.Equal(8m, r.TaxPercentage);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("12abc")]
        public void ReadRecords_NonNumericInNumberColumn_FailsTheWholeFile(string value)
        {
            Assert.ThrowsAny<Exception>(() => CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,Quantity\nINV-5,1\nINV-6," + value + "\n")));
        }

        [Fact]
        public void ReadRecords_ThousandsSeparatorInQuotedNumber_IsAccepted()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,UnitPrice\nINV-11,\"1,234.50\"\n")));
            Assert.Equal(1234.50m, r.UnitPrice);
        }

        [Fact]
        public void ReadRecords_QuotedCommasQuotesAndLineBreaks()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,ItemDescription\nINV-7,\"Bolt, M8 \"\"zinc\"\"\nsecond line\"\n")));

            Assert.Equal("Bolt, M8 \"zinc\"\nsecond line", r.ItemDescription);
        }

        [Fact]
        public void ReadRecords_SkipsBlankLines()
        {
            var rows = CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,Quantity\n\nINV-8,1\n\n\nINV-9,2\n\n"));

            Assert.Equal(new[] { "INV-8", "INV-9" }, rows.Select(r => r.InvoiceNo));
        }

        [Fact]
        public void ReadRecords_Utf8Bom_HeaderStillMatches()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv("InvoiceNo,Quantity\nINV-10,1\n", bom: true)));
            Assert.Equal("INV-10", r.InvoiceNo);
        }

        [Fact]
        public void ReadRecords_HeaderOnly_GivesNoRecords()
        {
            Assert.Empty(CsvImportHelper.ReadRecords<InvoiceRow>(Csv("InvoiceNo,Quantity\n")));
        }

        [Fact]
        public void ReadRecords_ShortRow_MissingTrailingCellsReadAsEmpty()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv(
                "InvoiceNo,ItemDescription,Quantity\nINV-12\n")));

            Assert.Equal("INV-12", r.InvoiceNo);
            Assert.Equal("", r.ItemDescription);
            Assert.Null(r.Quantity);
        }

        [Fact]
        public void ReadRecords_HeaderInDifferentLetterCase_FailsTheWholeFile()
        {
            Assert.ThrowsAny<Exception>(() => CsvImportHelper.ReadRecords<InvoiceRow>(Csv("invoiceno,QUANTITY\nINV-13,4\n")));
        }

        [Fact]
        public void ReadRecords_ItemImport_Columns()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<ItemRow>(Csv(
                "ClassificationCode,ItemCode,Description,UnitCode,UnitPrice\n022,SKU-1,Consulting,C62,150.00\n")));

            Assert.Equal("022", r.ClassificationCode);
            Assert.Equal("SKU-1", r.ItemCode);
            Assert.Equal("Consulting", r.Description);
            Assert.Equal("C62", r.UnitCode);
            Assert.Equal(150.00m, r.UnitPrice);
        }

        [Fact]
        public void ReadRecords_ItemImport_EmptyPrice_IsNull()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<ItemRow>(Csv(
                "ClassificationCode,ItemCode,Description,UnitPrice\n022,SKU-2,Thing,\n")));
            Assert.Null(r.UnitPrice);
        }

        [Fact]
        public void ReadRecords_BuyerImport_KeepsLeadingZerosAndPlus()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<BuyerRow>(Csv(
                "CompanyName,TIN,RegTypeCode,RegNo,PhoneNo,Address,CityName,StateCode,CountryCode,SST,TTX,MSIC\n" +
                "Acme Sdn Bhd,C1234567890,BRN,202301012345,+60376602222,\"1 Jalan A, Taman B\",Shah Alam,10,MYS,NA,NA,00000\n")));

            Assert.Equal("+60376602222", r.PhoneNo);
            Assert.Equal("10", r.StateCode);
            Assert.Equal("00000", r.MSIC);
            Assert.Equal("1 Jalan A, Taman B", r.Address);
        }

        [Fact]
        public void ReadRecords_CompanyImport_Columns()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<CompanyRow>(Csv(
                "CompanyName,TIN,RegTypeCode,RegNo,PhoneNo,Address,CityName,StateCode,CountryCode,SST,TTX,BankAccountNo\n" +
                "Beta Sdn Bhd,C9999999999,BRN,201901000001,+60311112222,Addr,KL,14,MYS,NA,NA,0123456789\n")));

            Assert.Equal("Beta Sdn Bhd", r.CompanyName);
            Assert.Equal("0123456789", r.BankAccountNo);
            Assert.Null(r.FaxNo);
        }

        [Fact]
        public void ReadRecords_RepeatedHeader_FirstColumnWins()
        {
            var r = Assert.Single(CsvImportHelper.ReadRecords<InvoiceRow>(Csv("InvoiceNo,InvoiceNo\nFIRST,SECOND\n")));
            Assert.Equal("FIRST", r.InvoiceNo);
        }

        private class IntRow { public int Count { get; set; } }

        [Fact]
        public void ReadRecords_UnsupportedPropertyType_FailsLoudly()
        {
            Assert.Throws<NotSupportedException>(() => CsvImportHelper.ReadRecords<IntRow>(Csv("Count\n1\n")));
        }

        [Fact]
        public void ReadRecords_BadNumber_MessageNamesTheColumn()
        {
            var ex = Assert.ThrowsAny<Exception>(() => CsvImportHelper.ReadRecords<InvoiceRow>(Csv("InvoiceNo,Quantity\nINV-14,two\n")));
            Assert.Contains("Quantity", ex.Message);
            Assert.Contains("two", ex.Message);
            Assert.Contains("Row 2:", ex.Message); // spreadsheet row: header is row 1
        }

        // ── ReadRows (Bulk Invoice import) ─────────────────────────────────────────────────────────

        [Fact]
        public void ReadRows_HeaderKeysAreTrimmedAndCaseInsensitive_ValuesTrimmed()
        {
            var row = Assert.Single(CsvImportHelper.ReadRows(Csv(" InvoiceNo ,Qty\n  INV-1  , 2 \n")));

            Assert.Equal("INV-1", row["invoiceno"]);
            Assert.Equal("2", row["QTY"]);
        }

        [Fact]
        public void ReadRows_SkipsAllEmptyRows_AndBlankHeaders()
        {
            var rows = CsvImportHelper.ReadRows(Csv("InvoiceNo,,Qty\nINV-1,x,1\n , , \nINV-2,y,2\n"));

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(2, r.Count)); // the blank-header column is dropped
            Assert.Equal("INV-2", rows[1]["InvoiceNo"]);
        }

        [Fact]
        public void ReadRows_EmptyFile_GivesNoRows()
        {
            Assert.Empty(CsvImportHelper.ReadRows(Csv("")));
        }

        [Fact]
        public void ReadRows_ShortRow_FailsTheWholeFile()
        {
            Assert.ThrowsAny<Exception>(() => CsvImportHelper.ReadRows(Csv("InvoiceNo,Qty\nINV-1\n")));
        }

        [Fact]
        public void ReadRows_QuotedValues()
        {
            var row = Assert.Single(CsvImportHelper.ReadRows(Csv("Desc,Qty\n\"a, \"\"b\"\"\",1\n")));
            Assert.Equal("a, \"b\"", row["Desc"]);
        }
    }
}
