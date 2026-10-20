using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    public class LogoUrlTests
    {
        [Theory]
        [InlineData(null, LogoUrl.Placeholder)]
        [InlineData("  ", LogoUrl.Placeholder)]
        [InlineData(@"E:\EINVWORLD_STAGING\Documents\Companies\Logos\IG209_logo_77_-18.png", "~/api/Image/logo?fileName=IG209_logo_77_-18.png")]
        [InlineData(@"D:\Logos\a b&c.png", "~/api/Image/logo?fileName=a%20b%26c.png")]
        [InlineData("~/uploads/logo.png", "~/uploads/logo.png")]
        [InlineData("/images/logo.png", "/images/logo.png")]
        [InlineData("https://cdn.example.com/l.png", "https://cdn.example.com/l.png")]
        public void MapsStoredPathToBrowserUrl(string? stored, string expected) =>
            Assert.Equal(expected, LogoUrl.For(stored));
    }
}
