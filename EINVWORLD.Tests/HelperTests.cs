using System;
using System.Threading.Tasks;
using eInvWorld.Helpers;          // DateTimeHelper
using EINVWORLD.Helpers;          // GeneralTINHelper, AmountInWordsHelper
using Xunit;

namespace EINVWORLD.Tests
{
    public class HelperTests
    {
        // ── InvoiceFormatting.FmtRate (exchange rate keeps every stored digit) ───────────────
        [Theory]
        [InlineData("4.567", "4.567")]
        [InlineData("4.123456", "4.123456")]
        [InlineData("1.000000", "1.00")]
        [InlineData("4.500000", "4.50")]
        public void FmtRate_KeepsStoredPrecision(string stored, string expected)
        {
            Assert.Equal(expected, InvoiceFormatting.FmtRate(decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Theory]
        [InlineData("1500.0000", "1500.00")]
        [InlineData("1.2345", "1.2345")]
        [InlineData("2.500000", "2.50")]
        [InlineData("0.123456", "0.123456")]
        public void FmtNum_KeepsStoredPrecision(string stored, string expected)
        {
            Assert.Equal(expected, InvoiceFormatting.FmtNum(decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void FmtRate_Null_IsEmpty() => Assert.Equal("", InvoiceFormatting.FmtRate(null));

        // ── InvoiceSubmissionGuard.OutcomeUnknown (keep the claim when LHDN may have accepted) ──
        [Fact]
        public void OutcomeUnknown_Only_For_Timeouts_Dropped_Connections_And_Gateway_Errors()
        {
            Assert.True(InvoiceSubmissionGuard.OutcomeUnknown(new TaskCanceledException("timeout")));
            Assert.True(InvoiceSubmissionGuard.OutcomeUnknown(new System.Net.Http.HttpRequestException("reset", new System.IO.IOException("conn reset"))));
            Assert.True(InvoiceSubmissionGuard.OutcomeUnknown(new System.Net.Http.HttpRequestException("gw", null, System.Net.HttpStatusCode.GatewayTimeout)));

            // LHDN answered: rejected / validation / rate limited / unavailable — safe to release and retry.
            Assert.False(InvoiceSubmissionGuard.OutcomeUnknown(new System.Net.Http.HttpRequestException("❌ Error submitting document: BadRequest")));
            Assert.False(InvoiceSubmissionGuard.OutcomeUnknown(new System.Net.Http.HttpRequestException("down", null, System.Net.HttpStatusCode.ServiceUnavailable)));
            Assert.False(InvoiceSubmissionGuard.OutcomeUnknown(new InvalidOperationException("no TIN")));
            Assert.False(InvoiceSubmissionGuard.OutcomeUnknown(new LhdnDocumentRejectedException("CF324"))); // claim released
        }

        // ── Durable job retry: never re-send a document LHDN rejected for validation errors ──
        [Fact]
        public void SubmitRetry_Skips_LhdnValidationRejection_But_Keeps_Transient_Failures()
        {
            Assert.False(EINVWORLD.Services.Background.DurableSyncJobWorker.ShouldRetry(1, 3, new LhdnDocumentRejectedException("CF324")));
            Assert.True(EINVWORLD.Services.Background.DurableSyncJobWorker.ShouldRetry(1, 3, new System.Net.Http.HttpRequestException("down", null, System.Net.HttpStatusCode.ServiceUnavailable)));
            Assert.True(EINVWORLD.Services.Background.DurableSyncJobWorker.ShouldRetry(2, 3, new InvalidOperationException("token")));
            Assert.False(EINVWORLD.Services.Background.DurableSyncJobWorker.ShouldRetry(3, 3, new InvalidOperationException("token")));
        }

        // ── Webhook SSRF private-address check ───────────────────────────────────────────────
        [Theory]
        [InlineData("10.1.2.3", true)]
        [InlineData("::ffff:10.1.2.3", true)]   // IPv4-mapped IPv6 used to slip through
        [InlineData("0.0.0.0", true)]
        [InlineData("100.64.0.1", true)]
        [InlineData("169.254.169.254", true)]
        [InlineData("8.8.8.8", false)]
        public void Webhook_IsPrivate(string ip, bool expected) =>
            Assert.Equal(expected, eInvWorld.Services.Webhooks.WebhookDeliveryJobHandler.IsPrivate(System.Net.IPAddress.Parse(ip)));

        // ── LogoImage.DetectExtension (PNG/JPEG only, by content) ────────────────────────────
        [Fact]
        public void LogoImage_Accepts_Png_And_Jpeg_Only_By_Content()
        {
            static string? Detect(params byte[] b) => LogoImage.DetectExtension(new System.IO.MemoryStream(b));
            Assert.Equal(".png", Detect(0x89, 0x50, 0x4E, 0x47, 0x0D));
            Assert.Equal(".jpg", Detect(0xFF, 0xD8, 0xFF, 0xE0));
            Assert.Null(Detect(System.Text.Encoding.UTF8.GetBytes("<svg onload=alert(1)>")));
            Assert.Null(Detect(0x47, 0x49, 0x46, 0x38)); // GIF not allowed
        }

        // ── GeneralTINHelper.IsGeneralTIN ─────────────────────────────────────────────────────
        [Theory]
        [InlineData("EI00000000010")] // General Public / Self-Billed Buyer
        [InlineData("EI00000000020")] // Foreign Buyer
        [InlineData("EI00000000030")] // Foreign Supplier
        [InlineData("EI00000000040")] // Government
        public void IsGeneralTIN_KnownGeneralTins_True(string tin)
        {
            Assert.True(GeneralTINHelper.IsGeneralTIN(tin));
        }

        [Theory]
        [InlineData("  EI00000000010  ")] // surrounding whitespace is trimmed
        [InlineData("EI00000000040 ")]
        public void IsGeneralTIN_TrimsWhitespace(string tin)
        {
            Assert.True(GeneralTINHelper.IsGeneralTIN(tin));
        }

        [Theory]
        [InlineData("C1234567890")]              // a normal taxpayer TIN
        [InlineData("EI00000000099")]            // not in the known set
        [InlineData("ei00000000010")]            // case-sensitive: lowercase is NOT a general TIN
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void IsGeneralTIN_NonGeneralOrEmpty_False(string? tin)
        {
            Assert.False(GeneralTINHelper.IsGeneralTIN(tin));
        }

        // ── LogSanitizer ──────────────────────────────────────────────────────────────────────
        [Fact]
        public void MaskTin_RealTin_KeepsFirst4Last2Only()
        {
            Assert.Equal("C123*****90", LogSanitizer.MaskTin("C1234567890"));
        }

        [Fact]
        public void MaskTin_GeneralTin_PassesThroughUnmasked()
        {
            // General LHDN TINs are public constants, not PII — keep logs readable.
            Assert.Equal("EI00000000010", LogSanitizer.MaskTin("EI00000000010"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MaskTin_NullOrBlank_ReturnsPlaceholder(string? tin)
        {
            Assert.Equal("(none)", LogSanitizer.MaskTin(tin));
        }

        [Fact]
        public void MaskId_ShortValue_FullyMasked()
        {
            Assert.Equal("******", LogSanitizer.MaskId("123456"));
        }

        [Fact]
        public void MaskId_LongValue_KeepsFirst4Last2()
        {
            Assert.Equal("2019********67", LogSanitizer.MaskId("20190123456767"));
        }

        // ── DateTimeHelper.ToMalaysiaTime ─────────────────────────────────────────────────────
        // Malaysia (MYT) is a fixed UTC+8 with no daylight saving, so the conversion is always +8h.
        [Theory]
        [InlineData("2026-06-24T00:00:00")]
        [InlineData("2026-01-01T12:34:56")]
        [InlineData("2026-12-31T23:00:00")]
        public void ToMalaysiaTime_AddsEightHours(string utcText)
        {
            var utc = DateTime.SpecifyKind(DateTime.Parse(utcText), DateTimeKind.Utc);

            var myt = DateTimeHelper.ToMalaysiaTime(utc);

            Assert.Equal(8, (myt - utc).TotalHours, 3); // precision: 3 decimal places
        }

        // ── DateTimeHelper.MalaysiaToUtc (invoice IssueDate/IssueTime sent to LHDN) ──────────
        [Fact]
        public void MalaysiaToUtc_SubtractsEightHours_WhateverTheServerZone()
        {
            var stored = new DateTime(2026, 10, 21, 11, 37, 0, DateTimeKind.Unspecified); // as read from the DB
            Assert.Equal(new DateTime(2026, 10, 21, 3, 37, 0), DateTimeHelper.MalaysiaToUtc(stored));
            Assert.Equal(new DateTime(2026, 10, 20, 23, 0, 0), DateTimeHelper.MalaysiaToUtc(new DateTime(2026, 10, 21, 7, 0, 0)));
        }

        [Fact]
        public void MalaysiaToUtc_LeavesUtcAlone_AndRoundTripsToMalaysiaTime()
        {
            var utc = new DateTime(2026, 10, 21, 3, 37, 0, DateTimeKind.Utc);
            Assert.Equal(utc, DateTimeHelper.MalaysiaToUtc(utc));
            Assert.Equal(utc, DateTimeHelper.MalaysiaToUtc(DateTimeHelper.ToMalaysiaTime(utc)));
        }

        // ── CsvExportHelper.EscapeCsv (formula injection + quoting) ──────────────────────────────
        [Theory]
        [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
        [InlineData("+1", "'+1")]
        [InlineData("-2", "'-2")]
        [InlineData("@SUM(A1)", "'@SUM(A1)")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("plain", "plain")]
        [InlineData(null, "")]
        public void EscapeCsv_NeutralisesFormulasAndQuotes(string? input, string expected)
            => Assert.Equal(expected, EINVWORLD.Helpers.CsvExportHelper.EscapeCsv(input));

        // ── AmountInWordsHelper.ToWordsEnglish ────────────────────────────────────────────────
        [Fact]
        public void ToWords_Zero_IsZeroOnly()
        {
            var words = AmountInWordsHelper.ToWordsEnglish(0m);

            Assert.Contains("Zero", words);
            Assert.EndsWith("Only", words);
            Assert.DoesNotContain("Cents", words); // no fractional part
        }

        [Fact]
        public void ToWords_WholeRinggit_NoCents()
        {
            var words = AmountInWordsHelper.ToWordsEnglish(150m);

            Assert.EndsWith("Only", words);
            Assert.DoesNotContain("Cents", words);
        }

        [Fact]
        public void ToWords_WithSen_IncludesCents()
        {
            var words = AmountInWordsHelper.ToWordsEnglish(5234.50m);

            Assert.Contains("Cents", words);
            Assert.EndsWith("Only", words);
        }

        [Fact]
        public void ToWords_OneSen_IsCounted()
        {
            // 0.01 must register as a single cent (decimal is exact here, no float drift).
            var words = AmountInWordsHelper.ToWordsEnglish(1.01m);

            Assert.Contains("Cents", words);
        }

        [Fact]
        public void ToWords_LargeAmount_UsesMillion()
        {
            var words = AmountInWordsHelper.ToWordsEnglish(1_234_567m);

            Assert.Contains("Million", words);
        }
    }
}
