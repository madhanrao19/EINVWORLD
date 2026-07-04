using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Fail-fast validation of the critical production settings, run once at startup.
    /// On-prem installs are deployed by non-developer admins; a blank connection string or a
    /// missing signing certificate should surface as ONE clear startup error listing every
    /// problem — not as a vague runtime exception hours later. Hard problems throw and stop
    /// the app; softer ones (preprod URL, missing SMTP password) are logged as warnings.
    /// </summary>
    public static class ProductionConfigValidator
    {
        /// <summary>
        /// Validates configuration. <paramref name="isProduction"/> tightens checks that only
        /// matter on a real server (DataProtection key ring, localhost URLs, preprod LHDN host).
        /// Throws <see cref="InvalidOperationException"/> aggregating all blocking problems.
        /// </summary>
        public static void Validate(IConfiguration config, bool isProduction)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            static bool Blank(string? v) => string.IsNullOrWhiteSpace(v);
            static bool IsLocalhost(string? v) =>
                !string.IsNullOrWhiteSpace(v) &&
                v.Contains("localhost", StringComparison.OrdinalIgnoreCase);

            // ── Database ──────────────────────────────────────────────────────────────────
            if (Blank(config["ConnectionStrings:DefaultConnection"]))
                errors.Add("ConnectionStrings:DefaultConnection is empty — set it via env var ConnectionStrings__DefaultConnection or user-secrets.");

            // ── Data Protection key ring ──────────────────────────────────────────────────
            // In-app fallback is fine for dev, but on a server a redeploy that clears App\ would
            // wipe the keys (logging out all users, breaking 2FA/antiforgery). Require a path.
            if (isProduction && Blank(config["DataProtection:KeyRingPath"]))
                errors.Add("DataProtection:KeyRingPath is empty — point it OUTSIDE the App folder (e.g. D:\\EINVWORLD\\Keys) so a redeploy does not wipe the keys.");

            // ── LHDN / MyInvois ───────────────────────────────────────────────────────────
            if (Blank(config["LHDNApiConfig:BaseUrl"]))
                errors.Add("LHDNApiConfig:BaseUrl is empty.");
            else if (isProduction && config["LHDNApiConfig:BaseUrl"]!.Contains("preprod", StringComparison.OrdinalIgnoreCase))
                warnings.Add("LHDNApiConfig:BaseUrl points at the PREPROD/sandbox host while ASPNETCORE_ENVIRONMENT=Production — switch to the production MyInvois host before going live.");

            // Digital signing: if turned on, the certificate must be fully specified or signing will crash at submit time.
            if (config.GetValue("LHDNApiConfig:SigningEnabled", false))
            {
                if (Blank(config["LHDNApiConfig:CertPath"]))
                    errors.Add("LHDNApiConfig:SigningEnabled=true but CertPath is empty.");
                if (Blank(config["LHDNApiConfig:CertPass"]))
                    errors.Add("LHDNApiConfig:SigningEnabled=true but CertPass is empty — set it via env var LHDNApiConfig__CertPass or user-secrets.");
            }

            // ── PDF generation ────────────────────────────────────────────────────────────
            if (isProduction && IsLocalhost(config["PDFGenerationSettings:BaseUrl"]))
                errors.Add("PDFGenerationSettings:BaseUrl is set to localhost in Production — set it to the server's public base URL.");

            // ── Email ─────────────────────────────────────────────────────────────────────
            if (isProduction && IsLocalhost(config["EmailConfiguration:EmailBaseUrls:BaseUrl"]))
                warnings.Add("EmailConfiguration:EmailBaseUrls:BaseUrl is localhost in Production — links in outgoing emails will be broken.");
            if (Blank(config["EmailConfiguration:Default:SmtpPassword"]))
                warnings.Add("EmailConfiguration:Default:SmtpPassword is empty — outgoing email will fail unless the relay accepts unauthenticated mail.");

            // ── AI (optional, off by default) ─────────────────────────────────────────────
            if (config.GetValue("AI:Enabled", false))
            {
                if (Blank(config["AI:BaseUrl"]))
                    errors.Add("AI:Enabled=true but AI:BaseUrl is empty (e.g. http://localhost:11434).");
                if (Blank(config["AI:Model"]))
                    errors.Add("AI:Enabled=true but AI:Model is empty (e.g. gemma3:12b).");
            }

            // ── Outbound webhooks (optional, off by default) ──────────────────────────────
            // No required secrets (per-subscription secrets are generated in-app), so these are
            // security-hygiene warnings, not blockers: flag when the SSRF/TLS guards are turned off
            // in Production, since webhook payloads carry invoice identifiers and go to
            // operator-configured URLs.
            if (config.GetValue("Webhooks:Enabled", false))
            {
                if (config.GetValue("Webhooks:DeliveryTimeoutSeconds", 15) <= 0)
                    warnings.Add("Webhooks:Enabled=true but Webhooks:DeliveryTimeoutSeconds is <= 0 — the delivery timeout falls back to 15s.");
                if (isProduction && !config.GetValue("Webhooks:RequireHttps", true))
                    warnings.Add("Webhooks:Enabled=true with Webhooks:RequireHttps=false in Production — signed payloads may be sent over plain HTTP. Only disable HTTPS for a trusted internal receiver.");
                if (isProduction && !config.GetValue("Webhooks:BlockPrivateNetworks", true))
                    warnings.Add("Webhooks:Enabled=true with Webhooks:BlockPrivateNetworks=false in Production — the SSRF guard is off. Only disable it for a trusted internal receiver on a private network.");
            }

            foreach (var w in warnings)
                Log.Warning("⚠️ Config check: {Warning}", w);

            if (errors.Count > 0)
            {
                var message = "Startup configuration validation failed:" + Environment.NewLine +
                              string.Join(Environment.NewLine, errors.ConvertAll(e => "  • " + e));
                throw new InvalidOperationException(message);
            }

            Log.Information("✅ Production configuration validated ({WarningCount} warning(s)).", warnings.Count);
        }
    }
}
