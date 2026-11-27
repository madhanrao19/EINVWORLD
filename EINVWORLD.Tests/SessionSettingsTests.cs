using System;
using eInvWorld.Models.Settings;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>"Remember me" gets the longer lifetime; a normal sign-in keeps the idle timeout.</summary>
    public class SessionSettingsTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Remembered_sign_in_lasts_RememberMeDays() =>
            Assert.Equal(Now.AddDays(14), new SessionSettings { RememberMeDays = 14 }.RememberMeExpiry(true, Now));

        [Fact]
        public void Normal_sign_in_keeps_the_default_expiry() =>
            Assert.Null(new SessionSettings { RememberMeDays = 14 }.RememberMeExpiry(false, Now));

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Zero_or_negative_days_turns_the_longer_lifetime_off(int days) =>
            Assert.Null(new SessionSettings { RememberMeDays = days }.RememberMeExpiry(true, Now));
    }
}
