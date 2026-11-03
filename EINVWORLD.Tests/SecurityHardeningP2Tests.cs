using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>Security pass 2 (FINDINGS.md #12, #13, #15, #16, #17): public forms, antiforgery, admin image uploads.</summary>
    public class SecurityHardeningP2Tests
    {
        public static IEnumerable<object[]> PublicForms() => new[]
        {
            typeof(eInvWorld.Areas.Identity.Pages.Account.LoginModel),
            typeof(eInvWorld.Areas.Identity.Pages.Account.RegisterModel),
            typeof(eInvWorld.Areas.Identity.Pages.Account.ForgotPasswordModel),
            typeof(eInvWorld.Areas.Identity.Pages.Account.ResendEmailConfirmationModel),
            typeof(eInvWorld.Pages.Lead.SubmitModel),
            typeof(eInvWorld.Pages.ContactModel),
        }.Select(t => new object[] { t });

        [Theory]
        [MemberData(nameof(PublicForms))]
        public void PublicForms_HaveThePerIpPostLimit(Type page)
        {
            var attr = (EnableRateLimitingAttribute?)Attribute.GetCustomAttribute(page, typeof(EnableRateLimitingAttribute), inherit: true);
            Assert.Equal("public-forms", attr?.PolicyName);
        }

        [Fact]
        public void CspReport_HasThePerIpPostLimit()
        {
            var method = typeof(eInvWorld.Controllers.CspReportController).GetMethods()
                .Single(m => m.GetCustomAttributes(typeof(HttpPostAttribute), false).Any());
            Assert.Equal("public-forms", ((EnableRateLimitingAttribute)Attribute.GetCustomAttribute(method, typeof(EnableRateLimitingAttribute))!).PolicyName);
        }

        [Theory]
        [InlineData(typeof(eInvWorld.Controllers.InvoiceController))]
        [InlineData(typeof(EINVWORLD.Controllers.ResourcesMigrationController))]
        public void CookieAuthenticatedApiPosts_ValidateAntiforgery(Type controller) =>
            Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(AutoValidateAntiforgeryTokenAttribute)));

        [Fact]
        public void ContactForm_RejectsOversizedFields()
        {
            var form = new eInvWorld.Pages.ContactFormModel
            {
                Name = "A", Company = "B", Telephone = "0123456789", Email = "a@example.com", Message = new string('x', 4001),
            };
            var errors = new List<ValidationResult>();
            Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true));
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(form.Message)));
        }

        [Fact]
        public void RegisterFullName_IsCappedAt100()
        {
            var prop = typeof(eInvWorld.Areas.Identity.Pages.Account.RegisterModel.InputModel).GetProperty("FullName")!;
            Assert.Equal(100, ((StringLengthAttribute)Attribute.GetCustomAttribute(prop, typeof(StringLengthAttribute))!).MaximumLength);
        }

        private static IFormFile Upload(byte[] bytes, string name = "x.png") =>
            new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", name);

        [Fact]
        public void ResourceImage_AcceptsPngAndJpeg()
        {
            Assert.Null(LogoImage.Reject(Upload(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0 }), LogoImage.MaxResourceImageBytes));
            Assert.Null(LogoImage.Reject(Upload(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "x.jpg"), LogoImage.MaxResourceImageBytes));
        }

        [Theory]
        [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "x.png")] // renamed SVG
        [InlineData("<html><script>alert(1)</script></html>", "x.jpg")]
        public void ResourceImage_RefusesAnythingElse_WhateverItsName(string content, string name) =>
            Assert.NotNull(LogoImage.Reject(Upload(System.Text.Encoding.UTF8.GetBytes(content), name), LogoImage.MaxResourceImageBytes));

        [Fact]
        public void ResourceImage_RefusesOversizedFiles()
        {
            var big = new byte[LogoImage.MaxResourceImageBytes + 1];
            big[0] = 0x89; big[1] = 0x50; big[2] = 0x4E; big[3] = 0x47;
            Assert.NotNull(LogoImage.Reject(Upload(big), LogoImage.MaxResourceImageBytes));
        }
    }
}
