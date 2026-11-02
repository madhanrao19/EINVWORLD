using eInvWorld.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    public class DefaultAddressTests
    {
        [Theory]
        [InlineData(null, null, "MYS", "10")]        // new form: Malaysia, Selangor
        [InlineData("", " ", "MYS", "10")]
        [InlineData("MYS", null, "MYS", "10")]       // Malaysian address with no state yet
        [InlineData("MYS", "14", "MYS", "14")]       // saved state kept
        [InlineData("SGP", null, "SGP", "")]         // foreign address: no Malaysian state forced on it
        [InlineData("SGP", "Central", "SGP", "Central")]
        [InlineData(null, "14", "MYS", "14")]
        public void DefaultAddress_FillsOnlyEmptyValues(string? country, string? state, string expCountry, string expState)
        {
            var (c, s) = DropdownHelper.DefaultAddress(country, state);
            Assert.Equal(expCountry, c);
            Assert.Equal(expState, s);
        }
    }
}
