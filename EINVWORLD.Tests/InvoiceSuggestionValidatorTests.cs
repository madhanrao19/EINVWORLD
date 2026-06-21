using System.Collections.Generic;
using EINVWORLD.Services.Assistant;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Covers the server-side validation that catches AI hallucinations before a suggestion is loaded
    /// into the Create Invoice form (bad LHDN codes, missing fields, non-positive amounts).
    /// </summary>
    public class InvoiceSuggestionValidatorTests
    {
        private static readonly HashSet<string> Classification = new() { "001", "022" };
        private static readonly HashSet<string> Tax = new() { "01", "06", "E" };

        private static InvoiceSuggestion ValidSuggestion() => new()
        {
            DocumentType = "01",
            DocumentTypeName = "Invoice",
            Currency = "MYR",
            BuyerName = "Acme Sdn Bhd",
            BuyerTin = "C1234567890",
            TaxType = "01",
            TaxRatePercent = 8,
            LineItems = new()
            {
                new SuggestionLine { Description = "Laptop", Quantity = 3, UnitPrice = 4500, ClassificationCode = "022" }
            }
        };

        [Fact]
        public void Review_ValidSuggestion_IsReadyForForm()
        {
            var review = InvoiceSuggestionValidator.Review(ValidSuggestion(), Classification, Tax);

            Assert.True(review.Parsed);
            Assert.False(review.HasErrors);
            Assert.True(review.ReadyForForm);
        }

        [Theory]
        [InlineData("99")]
        [InlineData("")]
        [InlineData(null)]
        public void Review_InvalidDocumentType_IsError(string? docType)
        {
            var s = ValidSuggestion();
            s.DocumentType = docType;

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.True(review.HasErrors);
            Assert.False(review.ReadyForForm);
        }

        [Fact]
        public void Review_UnknownClassificationCode_IsError()
        {
            var s = ValidSuggestion();
            s.LineItems[0].ClassificationCode = "999"; // not in the allowed set

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.True(review.HasErrors);
        }

        [Fact]
        public void Review_NoLineItems_IsError()
        {
            var s = ValidSuggestion();
            s.LineItems.Clear();

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.True(review.HasErrors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Review_NonPositiveQuantity_IsError(int qty)
        {
            var s = ValidSuggestion();
            s.LineItems[0].Quantity = qty;

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.True(review.HasErrors);
        }

        [Fact]
        public void Review_UnknownTaxType_IsWarningNotError()
        {
            var s = ValidSuggestion();
            s.TaxType = "ZZ"; // not a real tax code

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.False(review.HasErrors);   // tax mismatch is a warning, not blocking
            Assert.True(review.HasWarnings);
        }

        [Fact]
        public void Review_MissingBuyer_WarnsButDoesNotBlock()
        {
            var s = ValidSuggestion();
            s.BuyerName = null;
            s.BuyerTin = null;

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax);

            Assert.False(review.HasErrors);
            Assert.True(review.HasWarnings);
        }

        [Fact]
        public void Review_BuyerTinInSavedCustomers_NoBuyerWarning()
        {
            var s = ValidSuggestion();
            s.BuyerTin = "C1234567890";
            var known = new HashSet<string> { "C1234567890" };

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax, known);

            Assert.False(review.HasErrors);
            Assert.DoesNotContain(review.Items, i =>
                i.Severity == CheckSeverity.Warning && i.Message.Contains("not one of your saved customers"));
        }

        [Fact]
        public void Review_BuyerTinNotInSavedCustomers_Warns()
        {
            var s = ValidSuggestion();
            s.BuyerTin = "C9999999999";
            var known = new HashSet<string> { "C1234567890" };

            var review = InvoiceSuggestionValidator.Review(s, Classification, Tax, known);

            Assert.False(review.HasErrors); // a buyer mismatch is a warning, not blocking
            Assert.Contains(review.Items, i =>
                i.Severity == CheckSeverity.Warning && i.Message.Contains("not one of your saved customers"));
        }

        [Fact]
        public void TryParse_GoodJson_RoundTrips()
        {
            const string json = """
            {"documentType":"01","documentTypeName":"Invoice","currency":"MYR",
             "buyerName":"Acme","buyerTin":"C1","taxType":"01","taxRatePercent":8,
             "lineItems":[{"description":"Item","quantity":2,"unitPrice":10,"classificationCode":"022"}]}
            """;

            var parsed = InvoiceSuggestionValidator.TryParse(json);

            Assert.NotNull(parsed);
            Assert.Equal("01", parsed!.DocumentType);
            Assert.Single(parsed.LineItems);
            Assert.Equal(2, parsed.LineItems[0].Quantity);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParse_BadJson_ReturnsNull_AndReviewIsError(string? json)
        {
            var parsed = InvoiceSuggestionValidator.TryParse(json);
            Assert.Null(parsed);

            var review = InvoiceSuggestionValidator.Review(parsed, Classification, Tax);
            Assert.False(review.Parsed);
            Assert.True(review.HasErrors);
        }
    }
}
