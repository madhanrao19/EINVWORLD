// Tabler layout verification across every authenticated module, per role.
//
// PREREQUISITES to run this authenticated suite (it walks pages behind login):
//   1. The Tabler build (current `main`) must be DEPLOYED to the target
//      (EINVWORLD_BASE_URL) — otherwise it verifies the old Velzon UI and the
//      Tabler assertions fail by design.
//   2. Cloudflare Turnstile TEST keys must be configured so `login()` can pass:
//        Turnstile__SiteKey   = 1x00000000000000000000AA
//        Turnstile__SecretKey = 1x0000000000000000000000000000000AA
//   3. For the Admin arm, either the admin demo user has no 2FA, or
//        Security__EnforceAdminMfa = false   (temporarily, for QA only)
//
// What it checks per page: the Tabler shell rendered (.navbar-vertical present,
// Velzon .layout-wrapper absent), no same-origin console errors, no same-origin
// failed requests (>=400 / network), and no horizontal overflow at 375/768/1366/1920.

const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const WIDTHS = [
  { name: 'mobile', w: 375, h: 800 },
  { name: 'tablet', w: 768, h: 1024 },
  { name: 'laptop', w: 1366, h: 768 },
  { name: 'desktop', w: 1920, h: 1080 },
];

// Module inventory per role (authenticated pages migrated to Tabler).
const MODULES = {
  supplier: [
    ['Dashboard', '/Dashboard/Dashboard'],
    ['Invoice list (Sent)', '/Invoices/InvoiceLists?invoiceDirection=Sent'],
    ['Invoice list (Draft)', '/Invoices/InvoiceLists?invoiceDirection=Draft'],
    ['Create invoice', '/Invoices/CreateInvoice'],
    ['Bulk import', '/Invoices/ImportCsv'],
    ['Templates', '/Templates/TemplateLists'],
    ['Recurring invoices', '/RecurringInvoices/Index'],
    ['Items', '/Items/Index'],
    ['Create item', '/Items/Create'],
    ['Buyers', '/PublicCustomer/List'],
    ['Profile', '/Profile/Index'],
  ],
  buyer: [
    ['Dashboard', '/Dashboard/Dashboard'],
    ['Invoice list (Received)', '/Invoices/InvoiceLists?invoiceDirection=Received'],
    ['Profile', '/Profile/Index'],
  ],
  admin: [
    ['Dashboard', '/Dashboard/Dashboard'],
    ['Invoice list', '/Invoices/InvoiceLists'],
    ['Companies', '/Suppliers/Index'],
    ['Buyers', '/PublicCustomer/List'],
    ['Items', '/Items/Index'],
    ['Users', '/Admin/Users/ManageUser'],
    ['Leads', '/Lead/List'],
    ['Codes: State', '/Admin/Codes/StateCodes/ListState'],
    ['Codes: Tax types', '/Admin/Codes/TaxTypes/ListTaxType'],
    ['Codes: Classification', '/Admin/Codes/ClassificationCodes/ListClassification'],
    ['Sync jobs', '/Admin/SyncJobs'],
    ['Audit trail', '/Admin/AuditLog'],
    ['System health', '/Admin/SystemHealth'],
    ['Webhooks', '/Admin/Webhooks'],
    ['System logs', '/Admin/Logs/Index'],
    ['Manage resources', '/Admin/Resources/Manage'],
    ['AI settings', '/Admin/AiSettings'],
  ],
};

// Third-party analytics/telemetry that is out of the app's control and frequently times out on
// restricted networks (Google Tag Manager / Analytics, DoubleClick, Cloudflare Insights, and the
// Cloudflare GA proxy path). Failures here are NOT Tabler/app defects, so they are ignored.
const ANALYTICS = /googletagmanager|google-analytics|doubleclick|cloudflareinsights|ga-audiences|\/pxk8\/|\/g\/collect|\/csp-report/i;

// Same-origin error watcher keyed off the page's own host (works against staging, not just localhost).
function watchAppErrors(page) {
  const consoleErrors = [];
  const failedRequests = [];
  const appHost = () => { try { return new URL(page.url()).host; } catch { return ''; } };
  page.on('console', msg => {
    if (msg.type() !== 'error') return;
    const text = msg.text();
    if (/Failed to load resource/i.test(text)) return; // origin-less noise; covered by response handler
    if (ANALYTICS.test(text)) return;
    consoleErrors.push(text);
  });
  page.on('response', resp => {
    try {
      const u = new URL(resp.url());
      if (ANALYTICS.test(u.href)) return;
      if (u.host === appHost() && resp.status() >= 400) failedRequests.push(`${resp.status()} ${u.pathname}`);
    } catch { /* ignore */ }
  });
  page.on('requestfailed', req => {
    try {
      const u = new URL(req.url());
      if (ANALYTICS.test(u.href)) return;
      // ERR_ABORTED = the browser cancelled an in-flight request (the test navigates with 'commit'
      // and resizes fast, so late scripts get cancelled). That is a test artifact, not an app defect —
      // genuine failures surface as HTTP >=400 (response handler) or connection/DNS errors.
      const err = req.failure()?.errorText || '';
      if (/ERR_ABORTED/i.test(err)) return;
      if (u.host === appHost()) failedRequests.push(`FAILED ${u.pathname} (${err})`);
    } catch { /* ignore */ }
  });
  return { consoleErrors, failedRequests };
}

for (const [role, pages] of Object.entries(MODULES)) {
  test.describe(`Tabler modules — ${role}`, () => {
    test.describe.configure({ mode: 'serial' });

    for (const [name, path] of pages) {
      test(`${role}: ${name} (${path})`, async ({ page }) => {
        test.setTimeout(90000);
        const errs = watchAppErrors(page);

        await login(page, role);
        // 'commit' (not 'domcontentloaded') because blocked analytics hosts on staging can delay DCL
        // ~20s; the Tabler assertion below auto-waits for the layout to paint.
        const resp = await page.goto(path, { waitUntil: 'commit', timeout: 60000 });

        // Page loaded (not an error/redirect-to-login).
        expect(resp && resp.status(), `${path} HTTP status`).toBeLessThan(400);
        expect(page.url(), `${path} should not bounce to login`).not.toMatch(/\/login\b/i);

        // Tabler shell rendered, Velzon chrome gone.
        await expect(page.locator('aside.navbar-vertical'), 'Tabler sidebar present').toHaveCount(1);
        await expect(page.locator('.layout-wrapper'), 'Velzon layout-wrapper absent').toHaveCount(0);

        // No same-origin console errors or failed requests.
        expect(errs.consoleErrors, `console errors on ${path}`).toEqual([]);
        expect(errs.failedRequests, `failed requests on ${path}`).toEqual([]);

        // No *unusable* horizontal overflow at any breakpoint. Tolerance ~ one scrollbar width to
        // ignore sub-pixel/scrollbar rounding; a genuinely-too-wide element overflows by far more.
        for (const { name: bp, w, h } of WIDTHS) {
          await page.setViewportSize({ width: w, height: h });
          await page.waitForTimeout(150);
          const overflow = await page.evaluate(() =>
            document.documentElement.scrollWidth - document.documentElement.clientWidth);
          expect(overflow, `${path} horizontal overflow at ${bp} (${w}px)`).toBeLessThanOrEqual(17);
        }
      });
    }
  });
}
