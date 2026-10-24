using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>"Ready for LHDN": the rules every submit path now checks on the saved draft, one case each.</summary>
    public class InvoiceReadinessCheckTests
    {
        private static readonly DateTime Now = new(2026, 10, 24, 10, 0, 0);
        private static readonly ISet<string> Units = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "C62", "EA" };

        private static PartyInfo Party(string tin) => new()
        {
            CompanyName = "Co " + tin, TIN = tin, RegTypeCode = "BRN", RegNo = "202001000001", IndustryClassificationCode = "01111",
            BizDescription = "Trading", Addr1 = "1 Jalan", CityName = "KL", PostalCode = "50000", StateCode = "14",
            CountryCode = "MYS", PhoneNo = "+60123456789",
        };

        private static InvoiceLine Line(int no = 1) => new()
        {
            LineNumber = no, ItemDescription = "Service", Quantity = 1, UnitPrice = 100m, ClassificationCode = "022",
            UnitOfMeasure = "C62", AmountExclTax = 100m,
            InvoiceTaxes = new List<InvoiceTax> { new() { TaxCategory = "02", TaxPercentage = 8m, TaxAmount = 8m } },
        };

        private static InvoiceHeader Ready() => new()
        {
            InvoiceNo = "INV1", DocTypeCode = "01", Currency = "MYR", IssueDate = Now.AddHours(-1),
            Supplier = Party("C1111111111"), Customer = Party("C2222222222"),
            InvoiceLines = new List<InvoiceLine> { Line() }, TotalAmountExclTax = 100m, TotalTaxAmount = 8m,
        };

        private static List<ReadinessItem> Run(InvoiceHeader inv) => InvoiceReadinessCheck.Check(inv, Units, Now);

        [Fact]
        public void A_Complete_Draft_Is_Ready()
        {
            var items = Run(Ready());
            Assert.Empty(items);
            Assert.Null(InvoiceReadinessCheck.BlockingSummary(items));
        }

        [Fact]
        public void Missing_Parties_Point_To_The_Pickers()
        {
            var inv = Ready(); inv.Supplier = null!; inv.Customer = null;
            var items = Run(inv);
            Assert.Contains(items, i => i.Section == "Supplier" && i.Blocking && i.Field == "Invoice.SupplierId");
            Assert.Contains(items, i => i.Section == "Buyer" && i.Blocking && i.Field == "SelectedBuyerId");
        }

        [Fact]
        public void Party_Rules_Come_From_The_Mapper()
        {
            var inv = Ready(); inv.Customer!.PhoneNo = ""; inv.Supplier.PostalCode = null!;
            var items = Run(inv);
            Assert.Contains(items, i => i.Section == "Buyer" && i.Message.Contains("Phone Number"));
            Assert.Contains(items, i => i.Section == "Supplier" && i.Message.Contains("Postal Code"));
        }

        [Fact]
        public void Public_Customer_Is_Checked_When_There_Is_No_Registered_Buyer()
        {
            var inv = Ready(); inv.Customer = null;
            inv.PublicCustomer = new PublicCustomer
            {
                CompanyName = "Walk-in", TIN = "EI00000000010", RegNo = "NA", IndustryClassificationCode = "00000",
                Addr1 = "NA", CityName = "NA", CountryCode = "MYS", PhoneNo = "NA", // no state
            };
            Assert.Contains(Run(inv), i => i.Section == "Buyer" && i.Message.Contains("State Code"));
        }

        [Fact]
        public void Invoice_Level_Rules()
        {
            var inv = Ready(); inv.Currency = "USD"; inv.ExchangeRate = null; inv.DocTypeCode = "02"; inv.RefUUID = " ";
            inv.IssueDate = Now.AddDays(-4);
            var fields = Run(inv).Where(i => i.Section == "Invoice").Select(i => i.Field).ToList();
            Assert.Equal(new[] { "Invoice.ExchangeRate", "Invoice.RefUUID", "Invoice.IssueDate" }, fields);

            inv = Ready(); inv.IssueDate = null;
            Assert.Contains(Run(inv), i => i.Field == "Invoice.IssueDate");
        }

        [Fact]
        public void No_Items_Blocks()
        {
            var inv = Ready(); inv.InvoiceLines.Clear();
            Assert.Contains(Run(inv), i => i.Section == "Items" && i.Blocking);
        }

        [Fact]
        public void Line_Rules_Name_The_Field_By_Form_Index()
        {
            var inv = Ready();
            var bad = Line(2);
            bad.ItemDescription = ""; bad.Quantity = 0; bad.UnitPrice = null; bad.ClassificationCode = null!; bad.UnitOfMeasure = "XYZ";
            inv.InvoiceLines.Add(bad);
            var fields = Run(inv).Where(i => i.Blocking).Select(i => i.Field).ToList();
            Assert.Equal(new[]
            {
                "Invoice.InvoiceLines[1].ItemDescription", "Invoice.InvoiceLines[1].Quantity", "Invoice.InvoiceLines[1].UnitPrice",
                "Invoice.InvoiceLines[1].ClassificationCode", "Invoice.InvoiceLines[1].UnitOfMeasure",
            }, fields);
        }

        [Fact]
        public void Tax_Rules()
        {
            var inv = Ready();
            var noTax = Line(2); noTax.InvoiceTaxes.Clear();
            var bad06 = Line(3); bad06.InvoiceTaxes[0] = new InvoiceTax { TaxCategory = "06", TaxPercentage = 8m, TaxAmount = 8m };
            inv.InvoiceLines.Add(noTax); inv.InvoiceLines.Add(bad06);
            inv.TotalAmountExclTax = 300m; inv.TotalTaxAmount = 16m;
            var tax = Run(inv).Where(i => i.Section == "Tax").ToList();
            Assert.Equal(new[] { "Invoice.InvoiceLines[1].Taxes[0].TaxCategory", "Invoice.InvoiceLines[2].Taxes[0].TaxPercentage" }, tax.Select(i => i.Field));
            Assert.Contains("0%", tax[1].Message);
        }

        [Theory]
        [InlineData("11", "EI00000000010", "022", true)]
        [InlineData("11", "EI00000000010", "004", false)]
        [InlineData("11", "EI00000000030", "035", false)]
        [InlineData("11", "EI00000000030", "004", true)]
        [InlineData("01", "EI00000000010", "022", false)]   // only self-billed is restricted
        public void Self_Billed_General_Tin_Classification(string docType, string supplierTin, string code, bool blocked)
        {
            var inv = Ready(); inv.DocTypeCode = docType; inv.Supplier.TIN = supplierTin;
            inv.InvoiceLines.First().ClassificationCode = code;
            Assert.Equal(blocked, Run(inv).Any(i => i.Field == "Invoice.InvoiceLines[0].ClassificationCode"));
        }

        // Staging preprod, 24-25 Oct 2026: EINV100576 (real buyer + 004) came back ERR250; EINV100579/580 (General Public "NA" + 022)
        // came back Invalid while the panel said Ready.
        [Theory]
        [InlineData("01", "C2222222222", "202001000001", "004", true)]    // 004 with a real buyer
        [InlineData("01", "EI00000000010", "NA", "022", true)]            // consolidated buyer without 004
        [InlineData("01", "EI00000000010", "", "022", true)]
        [InlineData("01", "EI00000000010", "NA", "004", false)]           // proper consolidated e-Invoice
        [InlineData("01", "EI00000000010", "900101145566", "022", false)] // individual buyer with an NRIC
        [InlineData("01", "C2222222222", "202001000001", "022", false)]   // normal B2B
        [InlineData("02", "C2222222222", "202001000001", "004", true)]    // credit note follows the same rule
        public void Consolidated_Classification_Matches_The_Buyer(string docType, string buyerTin, string buyerId, string code, bool blocked)
        {
            var inv = Ready(); inv.DocTypeCode = docType; inv.RefUUID = "REF";
            inv.Customer!.TIN = buyerTin; inv.Customer.RegNo = buyerId;
            inv.InvoiceLines.First().ClassificationCode = code;
            Assert.Equal(blocked, Run(inv).Any(i => i.Blocking && i.Field == "Invoice.InvoiceLines[0].ClassificationCode"));
        }

        [Fact]
        public void Consolidated_Rule_Also_Reads_A_Public_Customer_Buyer()
        {
            var inv = Ready(); inv.Customer = null;
            inv.PublicCustomer = new PublicCustomer
            {
                CompanyName = "General Public", TIN = "EI00000000010", RegNo = "NA", IndustryClassificationCode = "00000",
                Addr1 = "NA", CityName = "NA", StateCode = "17", CountryCode = "MYS", PhoneNo = "NA",
            };
            Assert.Contains(Run(inv), i => i.Field == "Invoice.InvoiceLines[0].ClassificationCode" && i.Message.Contains("004"));
        }

        [Fact]
        public void Stale_Totals_Only_Warn_Because_Lhdn_Gets_Totals_From_The_Lines()
        {
            var inv = Ready(); inv.TotalAmountExclTax = 90m;
            var items = Run(inv);
            var totals = Assert.Single(items);
            Assert.Equal("Totals", totals.Section);
            Assert.False(totals.Blocking);
            Assert.Null(InvoiceReadinessCheck.BlockingSummary(items));
        }

        [Fact]
        public void Unit_Codes_Are_Only_Checked_When_The_Caller_Supplies_Them()
        {
            var inv = Ready(); inv.InvoiceLines.First().UnitOfMeasure = "XYZ";
            Assert.Empty(InvoiceReadinessCheck.Check(inv, null, Now));
            Assert.Contains(Run(inv), i => i.Field == "Invoice.InvoiceLines[0].UnitOfMeasure");
        }

        [Fact]
        public void Blocking_Summary_Lists_Every_Blocking_Message()
        {
            var inv = Ready(); inv.InvoiceLines.First().Quantity = 0; inv.IssueDate = null;
            var summary = InvoiceReadinessCheck.BlockingSummary(Run(inv))!;
            Assert.StartsWith("Not ready for LHDN:", summary);
            Assert.Contains("quantity", summary);
            Assert.Contains("issue date", summary);
        }
    }
}
