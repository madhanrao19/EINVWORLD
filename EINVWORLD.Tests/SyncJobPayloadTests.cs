using EINVWORLD.Services.Background;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Pure round-trip tests for the tiny JSON payload carried on a durable <c>SyncJob</c> row — no DB,
    /// no HTTP. Covers the pre-existing LookbackDays helpers plus the InvoiceNo helpers added for the
    /// SubmitDocument dead-letter/retry job type.
    /// </summary>
    public class SyncJobPayloadTests
    {
        [Fact]
        public void LookbackOrDefault_ValidPayload_ReturnsStoredValue()
        {
            var json = SyncJobPayload.Create(45);
            Assert.Equal(45, SyncJobPayload.LookbackOrDefault(json, 7));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        public void LookbackOrDefault_MissingOrBadPayload_ReturnsFallback(string? json)
        {
            Assert.Equal(7, SyncJobPayload.LookbackOrDefault(json, 7));
        }

        [Fact]
        public void LookbackOrDefault_ZeroOrNegative_ReturnsFallback()
        {
            var json = SyncJobPayload.Create(0);
            Assert.Equal(7, SyncJobPayload.LookbackOrDefault(json, 7));
        }

        [Fact]
        public void CreateForInvoice_RoundTrips_InvoiceNo()
        {
            var json = SyncJobPayload.CreateForInvoice("INV-00042");
            Assert.Equal("INV-00042", SyncJobPayload.InvoiceNoOrNull(json));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        public void InvoiceNoOrNull_MissingOrBadPayload_ReturnsNull(string? json)
        {
            Assert.Null(SyncJobPayload.InvoiceNoOrNull(json));
        }

        [Fact]
        public void InvoiceNoOrNull_LookbackOnlyPayload_ReturnsNull()
        {
            // A StatusSync/FullImport job's payload has no InvoiceNo — must not throw, just return null.
            var json = SyncJobPayload.Create(30);
            Assert.Null(SyncJobPayload.InvoiceNoOrNull(json));
        }

        [Fact]
        public void LookbackOrDefault_InvoiceOnlyPayload_ReturnsFallback()
        {
            // Symmetric case: a SubmitDocument job's payload has no LookbackDays.
            var json = SyncJobPayload.CreateForInvoice("INV-1");
            Assert.Equal(7, SyncJobPayload.LookbackOrDefault(json, 7));
        }
    }
}
