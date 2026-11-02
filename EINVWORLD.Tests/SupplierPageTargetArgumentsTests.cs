using System;
using System.Linq;
using System.Reflection;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// SupplierBasePage gates a company page on the handler argument named in TargetCompanyArguments. A typo or a
    /// renamed handler parameter would silently fall back to the user's own company, so every name must be a real
    /// int / int? parameter of one of that page's handlers.
    /// </summary>
    public class SupplierPageTargetArgumentsTests
    {
        private static readonly Type[] Pages = typeof(SupplierBasePage).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SupplierBasePage)) && !t.IsAbstract).ToArray();

        private static string[] TargetArguments(Type page)
        {
            var prop = page.GetProperty("TargetCompanyArguments", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(page);
            return (string[])prop.GetValue(instance)!;
        }

        [Fact]
        public void FindsTheSupplierPages() => Assert.True(Pages.Length >= 17, $"found {Pages.Length}");

        [Fact]
        public void EveryTargetArgument_IsAnIntParameterOfAHandler()
        {
            var problems = Pages.SelectMany(page =>
            {
                var handlerParams = page.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name.StartsWith("OnGet") || m.Name.StartsWith("OnPost"))
                    .SelectMany(m => m.GetParameters())
                    .Where(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(int?))
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);
                return TargetArguments(page).Where(a => !handlerParams.Contains(a)).Select(a => $"{page.Name}: '{a}'");
            }).ToList();
            Assert.True(problems.Count == 0, "Not an int handler parameter: " + string.Join(", ", problems));
        }

        [Theory]
        [InlineData("Users")]
        [InlineData("Details")]
        [InlineData("RolesPermissions")]
        [InlineData("InvoiceBranding")]
        [InlineData("Security")]
        [InlineData("Audit")]
        public void CompanySettingsPages_NameTheirTargetCompany(string pageName)
        {
            var page = Pages.Single(p => p.Namespace!.EndsWith(".Suppliers") && p.Name.StartsWith(pageName));
            Assert.NotEmpty(TargetArguments(page));
        }
    }
}
