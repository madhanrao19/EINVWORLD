using eInvWorld.Services.Middleware;
using Xunit;

namespace EINVWORLD.Tests
{
    public class CorrelationIdTests
    {
        [Theory]
        [InlineData("0HN7ABC:00000001", true)]
        [InlineData("3f2a9c1e-77d1-4b0a-9a0b-1c2d3e4f5a6b", true)]
        [InlineData("req_1.2", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("abc def", false)]                       // spaces: free text
        [InlineData("x\r\nFAKE LOG LINE", false)]            // log forging
        [InlineData("<script>alert(1)</script>", false)]
        public void IsValidId_AcceptsOnlyIdLikeValues(string? value, bool expected) =>
            Assert.Equal(expected, CorrelationIdMiddleware.IsValidId(value));

        [Fact]
        public void IsValidId_RejectsOver64Chars() =>
            Assert.False(CorrelationIdMiddleware.IsValidId(new string('a', 65)));
    }
}
