# CSP: from Report-Only to enforced (staged plan)

Status (v1.50.66, 14 Nov 2026): stages 0–4 are done. **Staging enforces** the policy (`Security__CspMode=Enforce` in
its `web.config`, QA'd for Admin, Supplier and Buyer). **Production is back on Report-Only**: it was enforced for about
10 minutes on 14 Nov and every Google Analytics hit stopped, because Production's only GA source is the two inline
loader scripts Cloudflare's Google tag gateway injects into each page, and they carry no nonce. v1.50.66 allows exactly
those two scripts by SHA-256 hash (`Security:CspScriptHashes`, Production settings only); Production is re-enforced
after that is deployed, with the owner's approval.

## Policy (v1.50.66)

```
default-src 'self'
script-src 'self' 'nonce-{per response}' [gateway loader hashes: Production only] https://challenges.cloudflare.com [https://www.googletagmanager.com: Production only]
script-src-attr 'none'
style-src / style-src-elem 'self' 'nonce-{…}' https://fonts.googleapis.com
style-src-attr 'unsafe-inline'                      <- remaining exception (stage 6)
font-src 'self' data: https://fonts.gstatic.com
img-src 'self' data: blob: [GA hosts: Production only]
connect-src 'self' [GA hosts: Production only]
frame-src 'self' blob: https://challenges.cloudflare.com https://www.google.com [GTM: Production only]
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
| 2. Inline handlers out | `onclick=` etc. → `data-on-click` + delegated listeners (`einv-actions.js`); `javascript:` links → buttons | **Done (v1.50.62)** |
| 3. No eval | Confirmed no page needs `eval`; runtime `<style>` elements removed from Invoice Lists | **Done (v1.50.64)** |
| 4. Enforce on Staging | `Security__CspMode=Enforce` in Staging `web.config`; PDF print fix (v1.50.65); Admin/Supplier/Buyer QA | **Done (14 Nov 2026)** |
| 5. Enforce on Production | Same switch on Production, **only with the owner's explicit approval**, watched for 24 h. First attempt rolled back (analytics); needs v1.50.66 deployed first | Waiting for v1.50.66 |
| 6. Styles | Move `style=` attributes to classes, then drop `style-src-attr 'unsafe-inline'` | Optional |

## Rollback

Set `Security__CspMode=ReportOnly` (or `Off`) in the site's `web.config` `<environmentVariables>`; the change
restarts the app and takes effect on the next request. No deploy needed.

## Monitoring

Reports land in `SystemLogs` as `CSP violation (distinct): directive=… blocked=… on=… source=…` at Information level,
de-duplicated per 10 minutes and rate-limited per IP. Query strings are stripped, so they hold no tokens or TINs.

## Cloudflare tag gateway hashes (Production)

Cloudflare's Google tag gateway inserts two inline `<script>` loaders at the top of every page (zone-wide, outside the
app) and sends GA hits through `/pxk8/` on our own origin. The scripts cannot carry our nonce, so Production lists their
SHA-256 hashes in `appsettings.Production.json` → `Security:CspScriptHashes`. `ContentSecurityPolicy.Build` adds only
well-formed `sha256-/sha384-/sha512-` values, so the setting cannot introduce `'unsafe-inline'`, hosts or wildcards.
Staging lists none, so its pages never send hits to the live GA property.

- Hashes were measured on 14 Nov 2026 from 10 page loads on each host; they were identical every time.
- Checked before merge: Production pages loaded with their policy switched to **enforced plus the two hashes** sent
  10 GA collects through `/pxk8/`. Still refused, and deliberately not allowed: the Google Ads remarketing pixel
  (`www.google.com.my/ads/ga-audiences`, not part of GA) and GA's `data:` feature probe (harmless).
- **If Cloudflare changes the loader**, the new hash appears in `/csp-report` (script-src, inline, on every page) and GA
  stops while the site keeps working. Add the new hash to `Security:CspScriptHashes` (or as env var
  `Security__CspScriptHashes__2` in `web.config`) and recycle.
