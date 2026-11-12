# Cloudflare Google tag gateway: Production analytics on Staging

Status: **open, needs a Google Tag Manager change by the account owner.** No application code is involved and none
should change: CSP stays Report-Only and the Playwright error watcher stays strict.

## What happens (verified 12 Nov 2026)

- The app turns Google Tag Manager **off** on Staging on purpose (`appsettings.Staging.json`:
  `GoogleAnalytics:MeasurementId` is empty), so QA traffic doesn't reach real analytics.
- Cloudflare's **Google tag gateway for advertisers** is enabled on the `einvworld.com` zone with the measurement path
  `/pxk8/`. Cloudflare injects the Google tag at the edge into every HTML page of the zone, so
  `https://staging.einvworld.com/About` contains `GTM-W2S2NPH7` (the Production container) and loads `/pxk8/`, even
  though the app's own HTML has no tag. Staging visits are therefore counted in Production analytics and Google Ads.
- `/pxk8/` never reaches IIS: an unknown path under it returns a Cloudflare 400 without the app's CSP header.
- **The console error:** the tag sometimes sends a diagnostics request (`/pxk8/?is_td=1&...&pid=733076776...`) as a
  `<script>`. Cloudflare answers it `204 No Content`, `Content-Type: text/plain`, with Cloudflare's own
  `X-Content-Type-Options: nosniff` (from the zone's HSTS/No-Sniff setting). Chrome refuses to execute a `text/plain`
  script under `nosniff`: *"Refused to execute script from 'https://staging.einvworld.com/pxk8/?is_td=1…'"*. It is
  intermittent because the diagnostics request is sampled. Production (`einvworld.com`, `www.einvworld.com`) returns
  exactly the same response.
- Not the cause: application code, the app's CSP (Report-Only never blocks; this is a MIME refusal), or Playwright.
  The watcher counts it because `/pxk8/` is on the app's own host; this is correct behaviour, so it is not filtered.

## Why it can't be fixed per hostname in Cloudflare

Cloudflare's documentation for Google tag gateway: the feature applies to all hostnames and subdomains in the zone, it
cannot be enabled or disabled for individual subdomains, and Configuration Rules cannot control or disable the tag
injection on specific subdomains. Cloudflare's recommended way to vary behaviour per subdomain is Google Tag Manager
triggers on Page Hostname. Source: <https://developers.cloudflare.com/google-tag-gateway/>.

Do **not** work around it by turning off Cloudflare's No-Sniff header, enforcing CSP, or filtering `/pxk8/` in the
Playwright watcher: each weakens a protection to hide a symptom.

## The change (Google Tag Manager, container `GTM-W2S2NPH7`)

Keep the gateway on for the zone; make the container fire tags only on the Production hostnames.

1. Variables: enable the built-in **Page Hostname** variable.
2. Triggers → New → **Custom Event** (or Page View) named `Block - non-production host`:
   - Event name: `.*`, *Use regex matching* ticked.
   - Fires on *Some Custom Events*: `Page Hostname` **does not match RegEx** `^(www\.)?einvworld\.com$`.
3. Add `Block - non-production host` as an **exception** (blocking trigger) on **every** tag in the container,
   including the Google tag / GA4 configuration tag and every Google Ads conversion and remarketing tag.
   (Equivalent alternative: give every firing trigger the condition `Page Hostname matches RegEx ^(www\.)?einvworld\.com$`.)
4. Preview on `https://staging.einvworld.com/` (Tag Assistant): no tags fire. Preview on `https://www.einvworld.com/`:
   tags fire as before.
5. Publish the container with a version note ("block non-production hostnames").

## Verify afterwards

- Tag Assistant on Staging shows no tags fired; GA4 Realtime shows no `staging.einvworld.com` page views.
- Run the public Playwright suite against Staging several times; the `/pxk8/?is_td=1` refusal should stop. If it still
  appears (the gateway script itself still loads on Staging, because injection is zone-wide), it no longer carries
  Staging traffic into Production analytics; report the `204 text/plain` + `nosniff` answer to Cloudflare support as a
  gateway defect rather than filtering it.

## Other options (not recommended)

- Turning the gateway off for the whole zone stops it on Production too.
- Moving Staging to a separate Cloudflare zone (its own domain) would exclude it fully but is a DNS/tunnel/certificate
  change across the deployment.
