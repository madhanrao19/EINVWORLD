using System.Security.Claims;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.ViewModels;
using EINVWORLD.Tests.Integration;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Invoice Edit builds the LHDN header for self-billed documents like Create does: the form's supplier (a general-TIN
    /// seller) is the Supplier and the user's own company is the buyer. It used to make the general-TIN party the buyer
    /// and always force EI00000000010 as seller (daily review 2026-11-26).
    /// </summary>
    public class InvoiceEditSelfBilledHeaderTests
    {
        [Theory]
        [InlineData("11", "EI00000000010")]
        [InlineData("12", "EI00000000030")]
        public void Self_Billed_Keeps_The_General_Tin_Seller_And_The_Own_Company_Buyer(string docType, string sellerTin)
        {
            var page = InvoiceEditLockedRedirectTests.Page(null!, new ClaimsPrincipal(), "POST");
            page.Invoice = new InvoiceHeaderView { InvoiceNo = "EINV00001", DocTypeCode = docType, Currency = "MYR" };
            var seller = new PartyInfo { PartyInfoId = 7, TIN = sellerTin };
            var ownCompany = new PartyInfo { PartyInfoId = 42, TIN = "C1111111111" };

            var header = page.CreateInvoiceHeader(seller, ownCompany, null);

            Assert.Same(seller, header.Supplier);
            Assert.Same(ownCompany, header.Customer);
            Assert.Equal(42, header.CustomerId);
            Assert.Null(header.PublicCustomer);
        }
    }
}
