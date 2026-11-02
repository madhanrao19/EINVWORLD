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

        [Fact]
        public void DefaultShippingAddress_NewRecipient_StartsOnMalaysiaSelangor()
        {
            var inv = new eInvWorld.Models.ViewModels.InvoiceHeaderView();
            DropdownHelper.DefaultShippingAddress(inv);
            Assert.Equal("MYS", inv.ShippingRecipientCountryCode);
            Assert.Equal("10", inv.ShippingRecipientState);
        }

        [Fact]
        public void DefaultShippingAddress_ExistingRecipientWithBlankState_IsLeftAlone()
        {
            // e.g. a CSV-imported draft: the state must not silently become Selangor and go to LHDN.
            var inv = new eInvWorld.Models.ViewModels.InvoiceHeaderView { ShippingRecipientName = "Penang Warehouse", ShippingRecipientAddrLine1 = "1 Jalan Penang" };
            DropdownHelper.DefaultShippingAddress(inv);
            Assert.Null(inv.ShippingRecipientCountryCode);
            Assert.Null(inv.ShippingRecipientState);
        }

        [Fact]
        public void DefaultShippingAddress_Null_DoesNothing() => DropdownHelper.DefaultShippingAddress(null);
    }
}
