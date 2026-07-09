using System;
using System.Text.Json;
using eInvWorld.Models.InputModel;
using eInvWorld.Services.Mappers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Exercises the UBL/JSON mapper end-to-end from an in-memory InvoiceHeader (no DB/HTTP). Guards the
    /// financial-correctness output (legal monetary totals + rounding), the doc-type BillingReference
    /// dispatch, party-identification filtering, and party validation.
    /// </summary>
    public class InvoiceMapperTests
    {
        // A PartyInfo with every field ValidatePartyInfo + MapSupplier require.
        private static PartyInfo ValidParty(string tin, string company) => new()
        {
            IndustryClassificationCode = "01111",
            BizDescription = "Growing of maize",
            CompanyName = company,
            TIN = tin,
            RegTypeCode = "BRN",
            RegNo = "201901234567",
            Addr1 = "Lot 1, Jalan Test",
            CityName = "Kuala Lumpur",
            StateCode = "14",
            PostalCode = "50000",
            CountryCode = "MYS",
            PhoneNo = "+60312345678",
            SST = "NA",   // "NA" must be filtered out of PartyIdentification
            TTX = "NA",
        };

        private static InvoiceLine LineWithTax(decimal qty, decimal price, decimal taxPercent)
        {
            var line = new InvoiceLine
            {
                InvoiceHeaderInvoiceNo = "INV-1",
                LineNumber = 1,
                ItemDescription = "Consulting",
                UnitOfMeasure = "C62",
                ClassificationCode = "022",
                Quantity = qty,
                UnitPrice = price,
            };
            line.InvoiceTaxes.Add(new InvoiceTax { TaxCategory = "01", TaxPercentage = taxPercent });
            line.CalculateAmounts(); // pre-compute TaxAmount so totals are deterministic
            return line;
        }

        private static InvoiceHeader Header(string docType, params InvoiceLine[] lines)
        {
            var header = new InvoiceHeader
            {
                InvoiceNo = "INV-1",
                DocTypeCode = docType,
                Currency = "MYR",
                IssueDate = new DateTime(2026, 6, 24, 1, 2, 3, DateTimeKind.Utc),
                Supplier = ValidParty("C1111111111", "Supplier Sdn Bhd"),
                Customer = ValidParty("C2222222222", "Buyer Sdn Bhd"),
                RefDocumentNo = "INV-ORIG-1",
            };
            foreach (var l in lines)
            {
                l.InvoiceHeader = header; // EF/production always set this back-reference; the mapper reads it.
                header.InvoiceLines.Add(l);
            }
            return header;
        }

        private static decimal Amount(JsonElement legalTotal, string field) =>
            legalTotal.GetProperty(field)[0].GetProperty("_").GetDecimal();

        // ── Totals & rounding ─────────────────────────────────────────────────────────────────
        [Fact]
        public void Map_StandardInvoice_ComputesLegalMonetaryTotals()
        {
            // Qty 10 × 100 = 1000 excl; 10% tax = 100; tax-inclusive = payable = 1100.
            var json = new InvoiceMapper().MapToJsonModel(Header("01", LineWithTax(10, 100, 10)));

            using var doc = JsonDocument.Parse(json);
            var invoice = doc.RootElement.GetProperty("Invoice")[0];
            Assert.Equal("01", invoice.GetProperty("InvoiceTypeCode")[0].GetProperty("_").GetString());
            Assert.Equal("MYR", invoice.GetProperty("DocumentCurrencyCode")[0].GetProperty("_").GetString());

            var lmt = invoice.GetProperty("LegalMonetaryTotal")[0];
            Assert.Equal(1000m, Amount(lmt, "LineExtensionAmount"));
            Assert.Equal(1000m, Amount(lmt, "TaxExclusiveAmount"));
            Assert.Equal(1100m, Amount(lmt, "TaxInclusiveAmount"));
            Assert.Equal(1100m, Amount(lmt, "PayableAmount"));
        }

        [Fact]
        public void Map_MultiLine_TotalsAreSummed()
        {
            // Line A: 3 × 33.33 = 99.99 excl, 6% = 5.9994 tax. Line B: 1 × 100 = 100 excl, 10% = 10 tax.
            var json = new InvoiceMapper().MapToJsonModel(
                Header("01", LineWithTax(3, 33.33m, 6), LineWithTax(1, 100, 10)));

            using var doc = JsonDocument.Parse(json);
            var lmt = doc.RootElement.GetProperty("Invoice")[0].GetProperty("LegalMonetaryTotal")[0];
            Assert.Equal(199.99m, Amount(lmt, "LineExtensionAmount"));     // 99.99 + 100
            // tax-inclusive = round(199.99 + (5.9994 + 10), 2) = round(215.9894, 2) = 215.99
            Assert.Equal(215.99m, Amount(lmt, "TaxInclusiveAmount"));
            Assert.Equal(215.99m, Amount(lmt, "PayableAmount"));
        }

        // ── BillingReference doc-type dispatch ────────────────────────────────────────────────
        [Fact]
        public void Map_DocType01_HasAdditionalRefOnly()
        {
            var json = new InvoiceMapper().MapToJsonModel(Header("01", LineWithTax(1, 10, 0)));

            using var doc = JsonDocument.Parse(json);
            var br = doc.RootElement.GetProperty("Invoice")[0].GetProperty("BillingReference")[0];
            Assert.True(br.TryGetProperty("AdditionalDocumentReference", out _));
            Assert.False(br.TryGetProperty("InvoiceDocumentReference", out _));
        }

        [Fact]
        public void Map_CreditNote02_HasInvoiceDocumentReference()
        {
            var json = new InvoiceMapper().MapToJsonModel(Header("02", LineWithTax(1, 10, 0)));

            using var doc = JsonDocument.Parse(json);
            var br = doc.RootElement.GetProperty("Invoice")[0].GetProperty("BillingReference")[0];
            Assert.True(br.TryGetProperty("InvoiceDocumentReference", out var idr));
            Assert.Equal("INV-ORIG-1", idr[0].GetProperty("ID")[0].GetProperty("_").GetString());
            Assert.False(br.TryGetProperty("AdditionalDocumentReference", out _));
        }

        [Fact]
        public void Map_SelfBilled11_HasBothBillingReferences()
        {
            // Self-billed docs (11–14) carry BOTH the invoice-document reference and the additional ref.
            var json = new InvoiceMapper().MapToJsonModel(Header("11", LineWithTax(1, 10, 0)));

            using var doc = JsonDocument.Parse(json);
            var invoice = doc.RootElement.GetProperty("Invoice")[0];
            Assert.Equal("11", invoice.GetProperty("InvoiceTypeCode")[0].GetProperty("_").GetString());

            var br = invoice.GetProperty("BillingReference")[0];
            Assert.True(br.TryGetProperty("InvoiceDocumentReference", out var idr));
            Assert.Equal("INV-ORIG-1", idr[0].GetProperty("ID")[0].GetProperty("_").GetString());
            Assert.True(br.TryGetProperty("AdditionalDocumentReference", out _));
        }

        // ── Party identification filtering ────────────────────────────────────────────────────
        [Fact]
        public void Map_FiltersNaSstAndKeepsTin()
        {
            var json = new InvoiceMapper().MapToJsonModel(Header("01", LineWithTax(1, 10, 0)));

            Assert.Contains("C1111111111", json); // supplier TIN present
            Assert.DoesNotContain("SST", json);   // "NA" SST/TTX filtered out entirely
        }

        // ── Party validation ──────────────────────────────────────────────────────────────────
        [Fact]
        public void Map_MissingSupplierTin_Throws()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Supplier.TIN = ""; // required field

            Assert.Throws<InvalidOperationException>(() => new InvoiceMapper().MapToJsonModel(header));
        }

        // ── SVDP document version (LHDN SDK, 8 Jul 2026; programme until 31 Dec 2027) ────────
        [Fact]
        public void Map_SvdpInvoice_EmitsListVersion12()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.IsSvdp = true;

            var json = new InvoiceMapper().MapToJsonModel(header);

            using var doc = JsonDocument.Parse(json);
            var typeCode = doc.RootElement.GetProperty("Invoice")[0].GetProperty("InvoiceTypeCode")[0];
            Assert.Equal("1.2", typeCode.GetProperty("listVersionID").GetString());
        }

        [Fact]
        public void Map_NormalInvoice_EmitsListVersion10()
        {
            var json = new InvoiceMapper().MapToJsonModel(Header("01", LineWithTax(1, 10, 0)));

            using var doc = JsonDocument.Parse(json);
            var typeCode = doc.RootElement.GetProperty("Invoice")[0].GetProperty("InvoiceTypeCode")[0];
            Assert.Equal("1.0", typeCode.GetProperty("listVersionID").GetString());
        }

        // ── Currency Exchange Rate (LHDN SDK, effective 1 Sep 2025) ──────────────────────────
        [Fact]
        public void Map_NonMyrWithoutExchangeRate_Throws()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Currency = "USD";
            header.ExchangeRate = null; // previously fell back silently to 1

            var ex = Assert.Throws<InvalidOperationException>(() => new InvoiceMapper().MapToJsonModel(header));
            Assert.Contains("Currency Exchange Rate", ex.Message);
        }

        [Fact]
        public void Map_NonMyrWithZeroExchangeRate_Throws()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Currency = "USD";
            header.ExchangeRate = 0m;

            Assert.Throws<InvalidOperationException>(() => new InvoiceMapper().MapToJsonModel(header));
        }

        [Fact]
        public void Map_NonMyrWithExchangeRate_EmitsTaxExchangeRate()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Currency = "USD";
            header.ExchangeRate = 4.7123m;

            var json = new InvoiceMapper().MapToJsonModel(header);

            using var doc = JsonDocument.Parse(json);
            var ter = doc.RootElement.GetProperty("Invoice")[0].GetProperty("TaxExchangeRate")[0];
            Assert.Equal("USD", ter.GetProperty("SourceCurrencyCode")[0].GetProperty("_").GetString());
            Assert.Equal("MYR", ter.GetProperty("TargetCurrencyCode")[0].GetProperty("_").GetString());
            Assert.Equal(4.7123m, ter.GetProperty("CalculationRate")[0].GetProperty("_").GetDecimal());
        }

        [Fact]
        public void Map_MyrWithoutExchangeRate_StillSucceeds()
        {
            // Regression guard: MYR invoices never needed an exchange rate and the payload
            // shape (MYR→MYR rate 1) must stay exactly as before.
            var json = new InvoiceMapper().MapToJsonModel(Header("01", LineWithTax(1, 10, 0)));

            using var doc = JsonDocument.Parse(json);
            var ter = doc.RootElement.GetProperty("Invoice")[0].GetProperty("TaxExchangeRate")[0];
            Assert.Equal(1m, ter.GetProperty("CalculationRate")[0].GetProperty("_").GetDecimal());
        }

        // ── State Code 17 restriction (LHDN SDK, effective 30 Apr 2026) ──────────────────────
        [Fact]
        public void Map_State17MalaysianCustomer_Throws()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Customer.StateCode = "17"; // "Not Applicable" — invalid for a domestic buyer

            var ex = Assert.Throws<InvalidOperationException>(() => new InvoiceMapper().MapToJsonModel(header));
            Assert.Contains("State Code 17", ex.Message);
        }

        [Fact]
        public void Map_State17ForeignCustomer_Succeeds()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Customer.StateCode = "17";
            header.Customer.CountryCode = "SGP"; // foreign party may use 17

            var json = new InvoiceMapper().MapToJsonModel(header);
            Assert.Contains("\"17\"", json);
        }

        [Fact]
        public void Map_State17GeneralTinCustomer_Succeeds()
        {
            var header = Header("01", LineWithTax(1, 10, 0));
            header.Customer.StateCode = "17";
            header.Customer.TIN = "EI00000000010"; // consolidated / general public buyer

            var json = new InvoiceMapper().MapToJsonModel(header);
            Assert.Contains("EI00000000010", json);
        }
    }
}
