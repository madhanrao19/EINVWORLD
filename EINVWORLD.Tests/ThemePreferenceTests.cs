using eInvWorld.Helpers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EINVWORLD.Tests
{
    public class ThemePreferenceTests
    {
        private static HttpContext WithCookie(string? value)
        {
            var ctx = new DefaultHttpContext();
            if (value != null) ctx.Request.Headers.Cookie = $"{ThemePreference.CookieName}={value}";
            return ctx;
        }

        [Theory]
        [InlineData("dark", "dark")]
        [InlineData("light", "light")]
        public void Read_AllowlistedValues(string cookie, string expected)
        {
            Assert.Equal(expected, ThemePreference.Read(WithCookie(cookie)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Dark")]                              // case-sensitive allowlist
        [InlineData("dark\" onload=\"alert(1)")]          // never reaches an HTML attribute
        [InlineData("system")]
        public void Read_AnythingElseIsNull(string? cookie)
        {
            Assert.Null(ThemePreference.Read(WithCookie(cookie)));
        }

        [Fact]
        public void Read_NullContextIsNull()
        {
            Assert.Null(ThemePreference.Read(null));
        }
    }
}
