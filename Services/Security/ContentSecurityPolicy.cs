using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace eInvWorld.Services.Security
{
    /// <summary>How the Content-Security-Policy is sent (<c>Security:CspMode</c>, read on every request).</summary>
    public enum CspMode { Off, ReportOnly, Enforce }

    /// <summary>
    /// Builds the site's Content-Security-Policy. Scripts run only from this origin, from the two third parties the
    /// pages really use (Cloudflare Turnstile; Google Tag Manager only where analytics is configured) or when they
    /// carry this response's nonce. There is no <c>'unsafe-inline'</c> or <c>'unsafe-eval'</c> for scripts, so inline
    /// event handlers (<c>onclick="…"</c>) and <c>javascript:</c> links are refused. Style attributes are the one
    /// remaining inline allowance (<c>style-src-attr</c>); style elements need the nonce.
    /// </summary>
    public static class ContentSecurityPolicy
    {
        /// <summary>HttpContext.Items key of this response's nonce (read by <c>CspNonceTagHelper</c>).</summary>
        public const string NonceItemKey = "csp-nonce";

        /// <summary>Name of the Reporting API endpoint declared in <c>Reporting-Endpoints</c>.</summary>
        public const string ReportGroup = "csp";

        /// <summary>A fresh 128-bit nonce, base64 (CSP requires base64 or base64url).</summary>
        public static string NewNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        /// <summary>This response's nonce, created once per request.</summary>
        public static string GetNonce(HttpContext context)
        {
            if (context.Items.TryGetValue(NonceItemKey, out var existing) && existing is string s) return s;
            var nonce = NewNonce();
            context.Items[NonceItemKey] = nonce;
            return nonce;
        }

        /// <summary>Parses <c>Security:CspMode</c>; anything unknown or empty means Report-Only (never silently off).</summary>
        public static CspMode ParseMode(string? value) =>
            Enum.TryParse<CspMode>(value?.Trim(), ignoreCase: true, out var mode) ? mode : CspMode.ReportOnly;

        /// <summary>The header name for a mode, or null when the policy is off.</summary>
        public static string? HeaderName(CspMode mode) => mode switch
        {
            CspMode.Enforce => "Content-Security-Policy",
            CspMode.ReportOnly => "Content-Security-Policy-Report-Only",
            _ => null
        };

        /// <param name="nonce">This response's nonce.</param>
        /// <param name="analytics">True only where Google Tag Manager is configured (Production); Staging gets none.</param>
        /// <param name="upgradeInsecure">True when the public site is HTTPS (not local development over HTTP).</param>
        /// <param name="scriptHashes">
        /// <c>Security:CspScriptHashes</c>: hashes (<c>sha256-…</c>) of inline scripts we do not render ourselves, so
        /// cannot nonce. Production lists the two loader scripts Cloudflare's Google tag gateway injects into every page.
        /// Anything that is not a well-formed hash is ignored, so this setting can never widen the policy.
        /// </param>
        public static string Build(string nonce, bool analytics, bool upgradeInsecure, IEnumerable<string>? scriptHashes = null)
        {
            const string turnstile = "https://challenges.cloudflare.com";
            var gtm = analytics ? " https://www.googletagmanager.com" : "";
            // GA4 sends hits to these; only listed where analytics is on.
            var gaConnect = analytics
                ? " https://www.googletagmanager.com https://*.google-analytics.com https://analytics.google.com https://*.analytics.google.com https://stats.g.doubleclick.net"
                : "";
            var gaImg = analytics ? " https://www.googletagmanager.com https://*.google-analytics.com" : "";

            var directives = new List<string>
            {
                "default-src 'self'",
                $"script-src 'self' 'nonce-{nonce}'{Hashes(scriptHashes)} {turnstile}{gtm}",
                // Inline handlers (onclick=…) are covered by script-src; with a nonce and no 'unsafe-inline' they are refused.
                "script-src-attr 'none'",
                $"style-src 'self' 'nonce-{nonce}' https://fonts.googleapis.com",
                $"style-src-elem 'self' 'nonce-{nonce}' https://fonts.googleapis.com",
                // shortcut: style="…" attributes stay allowed (291 in the views, plus libraries that set them); move them to classes to drop this.
                "style-src-attr 'unsafe-inline'",
                "font-src 'self' data: https://fonts.gstatic.com",
                $"img-src 'self' data: blob:{gaImg}",
                $"connect-src 'self'{gaConnect}",
                // Our own frames: Invoice Lists prints a PDF from a blob: URL in a hidden frame (printInvoicePdf).
                // Then Turnstile's challenge frame, Google Maps on the contact page and GTM's <noscript> frame.
                $"frame-src 'self' blob: {turnstile} https://www.google.com{gtm}",
                "worker-src 'self'",
                "manifest-src 'self'",
                "media-src 'self'",
                "object-src 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "frame-ancestors 'self'",
                "report-uri /csp-report",
                $"report-to {ReportGroup}"
            };
            if (upgradeInsecure) directives.Insert(directives.Count - 2, "upgrade-insecure-requests");
            return string.Join("; ", directives);
        }

        private static readonly Regex HashSource = new("^sha(256|384|512)-[A-Za-z0-9+/]+={0,2}$", RegexOptions.CultureInvariant);

        private static string Hashes(IEnumerable<string>? hashes) =>
            hashes == null ? "" : string.Concat(hashes.Select(h => h?.Trim().Trim('\'') ?? "")
                .Where(h => HashSource.IsMatch(h)).Distinct().Select(h => $" '{h}'"));
    }
}
