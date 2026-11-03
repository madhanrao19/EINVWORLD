using System;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pure tests for the LHDN "whose TIN/token acts on this document" rule — self-billed (11–14) uses the
    /// Customer's TIN, everything else the Supplier's. Getting this wrong submits under the wrong taxpayer.
    /// </summary>
    public class TinHelperTests
    {
        // ── IsSelfBilledDocType ───────────────────────────────────────────────────────────────
        [Theory]
        [InlineData("11")]
        [InlineData("12")]
        [InlineData("13")]
        [InlineData("14")]
        public void IsSelfBilled_SelfBilledCodes_True(string code) =>
            Assert.True(TinHelper.IsSelfBilledDocType(code));

        [Theory]
        [InlineData("01")]
        [InlineData("02")]
        [InlineData("03")]
        [InlineData("04")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("1")]   // partial match must NOT count
        [InlineData("111")]
        public void IsSelfBilled_NonSelfBilledOrEmpty_False(string? code) =>
            Assert.False(TinHelper.IsSelfBilledDocType(code));

        // ── ResolveSubmitterTin ───────────────────────────────────────────────────────────────
        private static InvoiceHeader Header(string docType, string? supplierTin, string? customerTin) => new()
        {
            DocTypeCode = docType,
            Supplier = supplierTin is null ? null! : new PartyInfo { TIN = supplierTin },
            Customer = customerTin is null ? null! : new PartyInfo { TIN = customerTin },
        };

        [Fact]
        public void Resolve_StandardInvoice_UsesSupplierTin()
        {
            var tin = TinHelper.ResolveSubmitterTin(Header("01", supplierTin: "C1111111111", customerTin: "C2222222222"));
            Assert.Equal("C1111111111", tin);
        }

        [Theory]
        [InlineData("11")]
        [InlineData("12")]
        [InlineData("13")]
        [InlineData("14")]
        public void Resolve_SelfBilled_UsesCustomerTin(string docType)
        {
            var tin = TinHelper.ResolveSubmitterTin(Header(docType, supplierTin: "C1111111111", customerTin: "C2222222222"));
            Assert.Equal("C2222222222", tin);
        }

        [Fact]
        public void Resolve_StandardWithNoSupplier_ReturnsNull()
        {
            var tin = TinHelper.ResolveSubmitterTin(Header("01", supplierTin: null, customerTin: "C2222222222"));
            Assert.Null(tin);
        }

        [Fact]
        public void Resolve_SelfBilledWithNoCustomer_ReturnsNull()
        {
            var tin = TinHelper.ResolveSubmitterTin(Header("11", supplierTin: "C1111111111", customerTin: null));
            Assert.Null(tin);
        }

        [Fact]
        public void Resolve_NullInvoice_Throws() =>
            Assert.Throws<ArgumentNullException>(() => TinHelper.ResolveSubmitterTin(null!));

        // Invoice Details sidebar: "Original invoice UUID" shows only for adjustment documents with a real reference.
        private const string Ref = "XKX4W7NYXMN9QEWQDNDREE6M10";

        [Theory]
        [InlineData("01", Ref, null)]          // standalone invoice: never, even if a stray RefUUID is stored
        [InlineData("11", Ref, null)]          // self-billed invoice
        [InlineData("02", Ref, Ref)]           // credit note
        [InlineData("03", Ref, Ref)]           // debit note
        [InlineData("04", Ref, Ref)]           // refund note
        [InlineData("12", Ref, Ref)]           // self-billed credit / debit / refund notes
        [InlineData("13", Ref, Ref)]
        [InlineData("14", Ref, Ref)]
        [InlineData("02", null, null)]         // missing reference: no empty row
        [InlineData("03", "", null)]
        [InlineData("04", "   ", null)]
        [InlineData("02", "NA", null)]         // LHDN's "no reference" placeholder
        [InlineData("12", "na", null)]
        [InlineData("02", " " + Ref + " ", Ref)]
        [InlineData(null, Ref, null)]
        public void DisplayReferenceUuid(string? docType, string? refUuid, string? expected) =>
            Assert.Equal(expected, TinHelper.DisplayReferenceUuid(docType, refUuid));
    }
}
