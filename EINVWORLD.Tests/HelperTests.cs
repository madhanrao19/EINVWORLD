using System;
using eInvWorld.Helpers;          // DateTimeHelper
using EINVWORLD.Helpers;          // GeneralTINHelper, AmountInWordsHelper
using Xunit;

namespace EINVWORLD.Tests
{
    public class HelperTests
    {
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
