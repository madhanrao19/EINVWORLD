using System;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>"Refresh from API" / status sync search window: whole Malaysia days sent to LHDN as UTC.</summary>
    public class LhdnSearchWindowTests
    {
        // 15 Nov 2026 00:30 MYT = 14 Nov 16:30 UTC: the old code sent from 15 Nov 00:00Z to 14 Nov 16:30Z and LHDN answered 400.
        [Fact]
        public void JustAfterMidnightMyt_TodayStartsAtMytMidnight_AndEndsNow()
        {
            var nowUtc = new DateTime(2026, 11, 14, 16, 30, 0, DateTimeKind.Utc);
            var today = new DateTime(2026, 11, 15);
            var (from, to) = LHDNApiService.SearchWindowUtc(today, today, nowUtc);
            Assert.Equal("2026-11-14T16:00:00Z", from);
            Assert.Equal("2026-11-14T16:30:00Z", to);
            Assert.True(string.CompareOrdinal(from, to) < 0);
            // An invoice issued at 00:20 MYT (16:20Z) is inside the window; it used to fall outside every window.
            Assert.InRange("2026-11-14T16:20:00Z", from, to);
        }

        [Fact]
        public void PastDays_CoverWholeMytDays()
        {
            var nowUtc = new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc);
            var (from, to) = LHDNApiService.SearchWindowUtc(new DateTime(2026, 11, 12), new DateTime(2026, 11, 14), nowUtc);
            Assert.Equal("2026-11-11T16:00:00Z", from);
            Assert.Equal("2026-11-14T15:59:59Z", to);
        }

        [Fact]
        public void LocalKindDates_AreTreatedAsMalaysiaDates()
        {
            var nowUtc = new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc);
            var local = new DateTime(2026, 11, 15, 9, 41, 0, DateTimeKind.Local);
            Assert.Equal(("2026-11-14T16:00:00Z", "2026-11-15T02:00:00Z"), LHDNApiService.SearchWindowUtc(local, local, nowUtc));
        }
    }
}
