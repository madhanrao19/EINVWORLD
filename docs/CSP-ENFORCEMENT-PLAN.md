# CSP: from Report-Only to enforced (staged plan)

Status (v1.50.61, 14 Nov 2026): the **strict candidate policy ships in Report-Only** on every response
(`ContentSecurityPolicy.Build`, `Security:CspMode`). Nothing is blocked. Stages 0 and 1 below are done.

## Policy (v1.50.61)

```
default-src 'self'
script-src 'self' 'nonce-{per response}' https://challenges.cloudflare.com [https://www.googletagmanager.com: Production only]
script-src-attr 'none'
style-src / style-src-elem 'self' 'nonce-{…}' https://fonts.googleapis.com
style-src-attr 'unsafe-inline'                      <- remaining exception (stage 6)
font-src 'self' data: https://fonts.gstatic.com
img-src 'self' data: blob: [GA hosts: Production only]
connect-src 'self' [GA hosts: Production only]
frame-src https://challenges.cloudflare.com https://www.google.com [GTM: Production only]
worker-src / manifest-src / media-src 'self'
object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'
upgrade-insecure-requests (HTTPS only); report-uri /csp-report; report-to csp
```

Removed compared with the old Report-Only policy: `'unsafe-inline'` and `'unsafe-eval'` for scripts, the unused CDN
hosts (jsdelivr, cdnjs, code.jquery, datatables, tiny.cloud), the `https:` wildcard for images, `www.google.com(.my)`
and `cdn.tiny.cloud` from `connect-src`, and every analytics host on Staging.

## Audit (14 Nov 2026)

| Dependency | Count | Handled by |
|---|---|---|
| Inline `<script>` blocks | 83 in 45 views (+ Identity area) | Nonce via `CspNonceTagHelper` (done) |
| `<style>` blocks | 12 | Nonce (done) |
| Inline event handlers (`onclick=` …) in views | 129 in 46 views | Stage 2 |
| Inline handlers built in JavaScript strings | 12 in `create-invoice.js`, 1 in `invoice-submit-core.js` | Stage 2 |
| `href="javascript:…"` | 9 | Stage 2 |
| `eval` / `new Function` | `lord-icon-2.1.0.js` and `card.js` (legacy Velzon, marketing layout), SweetAlert2 `.all` bundle | Stage 3 (check reports) |
| `style="…"` attributes | 291 | Stage 6 (exception until then) |
| Third parties | Turnstile (script + frame), GTM/GA4 (Production only), Google Maps embed (contact) | Allowed by name |

`Views/Invoices/PdfTemplate_v2.cshtml` is rendered to a string for the headless PDF renderer and never reaches a
browser, so the CSP does not apply to it.

## Stages

Each stage is one small PR, with a signed-in QA of the Finance workflows on Staging before it is promoted.

| Stage | Change | Status |
|---|---|---|
| 0. Tidy | Unused hosts removed, `Reporting-Endpoints` + `report-to`, page path/source/line in reports, query strings stripped | **Done (v1.50.61)** |
| 1. Nonces | Per-response nonce, tag helper on every `<script>`/`<style>`, candidate policy as the Report-Only header | **Done (v1.50.61)** |
| 2. Inline handlers out | `onclick=` etc. → `data-action` + delegated listeners; `javascript:` links → buttons. Low-traffic pages first, `InvoiceEdit` and the Create steps last, each with its own QA | Next |
| 3. No eval | Confirm from reports that nothing needs `eval`; replace or drop the legacy Velzon plugins on the marketing layout | After 2 |
| 4. Enforce on Staging | `Security__CspMode=Enforce` in Staging `web.config` once reports are at zero for the signed-in and public pages; full regression | After 2–3 |
| 5. Enforce on Production | Same switch on Production, **only with the owner's explicit approval**, watched for 24 h | After 4 |
| 6. Styles | Move `style=` attributes to classes, then drop `style-src-attr 'unsafe-inline'` | Optional |

## Rollback

Set `Security__CspMode=ReportOnly` (or `Off`) in the site's `web.config` `<environmentVariables>`; the change
restarts the app and takes effect on the next request. No deploy needed.

## Monitoring

Reports land in `SystemLogs` as `CSP violation (distinct): directive=… blocked=… on=… source=…` at Information level,
de-duplicated per 10 minutes and rate-limited per IP. Query strings are stripped, so they hold no tokens or TINs.
