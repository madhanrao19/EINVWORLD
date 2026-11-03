using System;
using System.Collections.Generic;
using System.IO;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Mappers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Security pass 1 (FINDINGS.md #2, #29): what an anonymous lead may set, which files a stored logo path
    /// may read into a PDF, and invoice-number file paths.
    /// </summary>
    public class SecurityHardeningP1Tests
    {
        [Fact]
        public void PublicLead_KeepsTheFormFields_AndDropsEverythingElse()
        {
            var posted = new PartyInfo
            {
                CompanyName = "Example Sdn Bhd", TIN = "C1234567890", RegNo = "202001000001", Email = "info@example.com",
                CountryCode = "MYS", StateCode = "10", BizDescription = null!,
                // Never settable from the public form:
                LogoPath = @"C:\inetpub\secret.png", InviteCode = "ABCD1234", IsActive = false, IsApproved = true,
                IsAdminCreated = true, BankAccountNo = "1234567890", InvoiceFooterNote = "x", Remarks = "x",
                LhdnIntermediaryRejected = true, CreatedBy = "attacker",
                UserCompanies = new List<UserCompany> { new() { UserId = "attacker-id", HasCompanyAccess = true } },
                AssignedBuyers = new List<SupplierBuyer> { new() { BuyerId = 1 } },
            };

            var lead = eInvWorld.Pages.Lead.SubmitModel.NewLeadFrom(posted);

            Assert.Equal("Example Sdn Bhd", lead.CompanyName);
            Assert.Equal("C1234567890", lead.TIN);
            Assert.Equal("info@example.com", lead.Email);
            Assert.Equal("", lead.BizDescription);
            Assert.Null(lead.LogoPath);
            Assert.Null(lead.InviteCode);
            Assert.Null(lead.BankAccountNo);
            Assert.Null(lead.InvoiceFooterNote);
            Assert.Null(lead.Remarks);
            Assert.True(lead.IsActive);
            Assert.False(lead.IsApproved);
            Assert.False(lead.IsAdminCreated);
            Assert.False(lead.LhdnIntermediaryRejected);
            Assert.Equal("CustomerInfo", lead.CreatedBy);
            Assert.Empty(lead.UserCompanies);
            Assert.Empty(lead.AssignedBuyers);
            Assert.Empty(lead.AssignedSuppliers);
        }

        [Theory]
        [InlineData("/api/companies/logos/a1.png", "logos", "a1.png")]
        [InlineData(@"E:\EINVWORLD\Logos\a1.jpg", "logos", "a1.jpg")]          // legacy absolute path: by name, in the logos folder
        [InlineData("/assets/images/users/multi-user.jpg", "web", @"assets\images\users\multi-user.jpg")]
        [InlineData("~/images/logo.png", "web", @"images\logo.png")]
        [InlineData(@"C:\Windows\win.ini", null, null)]                       // not an image
        [InlineData("/../appsettings.json", null, null)]
        [InlineData("/../../secret.png", null, null)]                          // traversal out of wwwroot
        [InlineData("/images/../../x.png", null, null)]                        // traversal mid-path
        [InlineData("/api/companies/logos/../../x.png", "logos", "x.png")]     // file name only, inside the folder
        [InlineData("http://169.254.169.254/latest.png", null, null)]          // never a URL for the PDF engine to fetch
        [InlineData("file:///C:/x.png", null, null)]
        [InlineData(@"\\server\share\x.png", null, null)]
        [InlineData("", null, null)]
        public void LogoPath_ResolvesOnlyToImagesInsideTheLogosFolderOrWwwroot(string logoPath, string? root, string? relative)
        {
            var logos = Path.Combine(Path.GetTempPath(), "einv-logos");
            var web = Path.Combine(Path.GetTempPath(), "einv-web");

            var resolved = InvoicePdfMapper.ResolveLogoFile(logoPath, logos, web);

            if (root == null) Assert.Null(resolved);
            else Assert.Equal(Path.GetFullPath(Path.Combine(root == "logos" ? logos : web, relative!)), resolved);
        }

        [Fact]
        public void LogoPath_WithoutALogosFolder_IsNotResolved() =>
            Assert.Null(InvoicePdfMapper.ResolveLogoFile("/api/companies/logos/a.png", null, Path.GetTempPath()));

        [Theory]
        [InlineData(@"..\secret")]
        [InlineData("../secret")]
        [InlineData(@"C:\x")]
        public void InvoiceNumberFilePath_RefusesTraversal(string invoiceNo) =>
            Assert.Throws<ArgumentException>(() => SafePath.FileIn(Path.GetTempPath(), $"{invoiceNo}.json"));

        [Fact]
        public void CompanyCreate_StaysOpenToSuppliers_OnlyForTinValidation()
        {
            // Suppliers/Edit calls Create's ValidateTIN handler; OnGet/OnPost refuse non-Admins themselves.
            var attr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
                typeof(eInvWorld.Pages.Suppliers.CreateModel), typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
            Assert.Equal("Admin,Supplier", attr.Roles);
        }

        [Fact]
        public void InvoiceNumberFilePath_StaysInTheFolder() =>
            Assert.Equal(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "EINV00001.json"), SafePath.FileIn(Path.GetTempPath(), "EINV00001.json"));
    }
}
