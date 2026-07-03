# Security Policy

EINVWORLD is a Malaysia LHDN MyInvois e-invoicing middleware handling financial and tax-identity data
(TINs, invoices, digital signatures). We take security reports seriously and want to make it easy to
report a problem responsibly.

## Reporting a vulnerability

**Do not open a public GitHub issue for a security vulnerability.** Instead, email:

> **security@einvworld.com** *(replace with your organisation's actual monitored security inbox before
> publishing this file — see the note at the bottom)*

Include, if possible:
- A description of the vulnerability and its potential impact.
- Steps to reproduce (a proof-of-concept is welcome but not required).
- The affected version (`AppInfo:Version` — visible to Admin users in the app footer, or check
  `appsettings.json`).
- Whether you believe customer data (invoices, TINs, certificates) may have been affected.

We aim to acknowledge reports within **3 business days** and to provide a remediation timeline within
**10 business days** of confirming the issue. Please give us a reasonable window to fix a confirmed
vulnerability before any public disclosure.

## Supported versions

EINVWORLD is deployed as a single on-prem instance per customer (no multi-version support matrix). Only
the **currently deployed version** on a given customer's server is in scope for that customer's reports —
always upgrade to the latest release before reporting, per `CHANGELOG.md` and
`DEPLOY-NOTES.md` §0 (upgrade checklist).

## Scope

In scope:
- The EINVWORLD application itself (this repository): authentication/authorization, LHDN
  submission/signing logic, data handling, the admin console, the optional AI features.
- Deployment guidance in `IIS-DEPLOYMENT-GUIDE.md`, `DEPLOY-NOTES.md`, and `SECRETS-SETUP.md`, if followed
  as documented and a resulting misconfiguration is exploitable.

Out of scope:
- Third-party services this app integrates with (LHDN MyInvois itself, SMTP providers, Ollama) — report
  those to their respective owners.
- Findings that require an attacker to already have valid Admin credentials or physical server access.
- Denial-of-service via sheer volume (rate limiting is a known, evolving area — see
  `README.md`'s single-instance rate-limiter note).

## What we consider a vulnerability here

Given the domain, these categories are treated as **high priority**:
- Any way to view, submit, cancel, or reject an invoice belonging to a different company/TIN than the
  authenticated user's own (an IDOR/object-level-authorization bypass — see `Helpers/UserExtensions.cs`
  for the intended enforcement).
- Any way to exfiltrate LHDN client secrets, the signing certificate/private key, or another tenant's
  TIN/address/bank-account data.
- Any way to forge, replay, or tamper with an audit-trail entry
  (`Services/Audit/AuditService.cs`'s hash chain).
- Authentication/session bypass, including 2FA bypass for Admin accounts.

## Handling of reports

Confirmed vulnerabilities are fixed in a dedicated, CI-gated PR (per `CLAUDE.md`'s engineering loop),
noted in `CHANGELOG.md` at a level of detail that doesn't itself constitute a disclosure advisory before a
fix has shipped to affected deployments, and credited to the reporter if they wish.

---
*Maintainers: replace the placeholder email above with a real, monitored inbox (and consider a PGP key)
before relying on this policy in production.*
