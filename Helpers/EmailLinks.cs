using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Absolute links for emails (password reset, email confirmation, invitations). Built from the configured
    /// EmailConfiguration:EmailBaseUrls:BaseUrl, never from the request's Host header: with AllowedHosts "*", anyone
    /// reaching IIS directly could otherwise send a reset email whose link (and token) points at their own domain.
    /// </summary>
    public static class EmailLinks
    {
        public const string BaseUrlKey = "EmailConfiguration:EmailBaseUrls:BaseUrl";

        /// <summary>Joins a relative URL from Url.Page/Url.Action with the configured site address.</summary>
        public static string Absolute(IConfiguration configuration, string? relativeUrl)
        {
            var baseUrl = configuration[BaseUrlKey];
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException($"{BaseUrlKey} must be set to the site's address; email links are never built from the request host.");
            if (string.IsNullOrEmpty(relativeUrl))
                throw new InvalidOperationException("The email link's page could not be resolved.");
            return baseUrl.TrimEnd('/') + "/" + relativeUrl.TrimStart('/');
        }

        /// <summary>Same, for a page model: <c>this.EmailLink(Url.Page(...))</c> without a protocol argument.</summary>
        public static string EmailLink(this PageModel page, string? relativeUrl) =>
            Absolute(page.HttpContext.RequestServices.GetRequiredService<IConfiguration>(), relativeUrl);
    }
}
