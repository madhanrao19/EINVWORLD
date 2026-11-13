using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Recurring Invoices is a supplier feature. Its pages used to require only a signed-in user, so a Buyer could
    /// open the list by typing the address (seen on Production, 13 Nov 2026).
    /// </summary>
    public class RecurringInvoicesRoleGateTests
    {
        [Theory]
        [InlineData(typeof(eInvWorld.Pages.RecurringInvoices.IndexModel))]
        [InlineData(typeof(eInvWorld.Pages.RecurringInvoices.CreateModel))]
        [InlineData(typeof(eInvWorld.Pages.RecurringInvoices.EditModel))]
        [InlineData(typeof(eInvWorld.Pages.Templates.TemplateListsModel))]
        public void Page_Is_For_Admin_And_Supplier_Only(Type page)
        {
            var roles = page.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).ToList();
            Assert.Contains("Admin,Supplier", roles);
            Assert.DoesNotContain(roles, r => r != null && r.Contains("Buyer"));
        }
    }
}
