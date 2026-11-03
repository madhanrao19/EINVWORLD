using System;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>FINDINGS.md #27: password-reset links have their own, shorter lifetime.</summary>
    public class PasswordResetTokenProviderTests
    {
        [Fact]
        public void Reset_Links_Live_Two_Hours_Under_Their_Own_Provider()
        {
            var o = new PasswordResetTokenProviderOptions();
            Assert.Equal(TimeSpan.FromHours(2), o.TokenLifespan);
            Assert.Equal("PasswordReset", o.Name);
            Assert.Equal(TimeSpan.FromDays(1), new Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions().TokenLifespan); // confirmation links keep a day
        }
    }
}
