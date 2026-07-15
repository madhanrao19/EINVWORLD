// Visual audit for the "EinvWorld Professional" restyle. Captures the redesigned pages at the
// required breakpoints for design review, and asserts no horizontal overflow on the auth pages.
// Skipped in normal runs — enable with RUN_VISUAL_AUDIT=1:
//   RUN_VISUAL_AUDIT=1 EINVWORLD_BASE_URL=http://localhost:5260 npx playwright test tests/playwright/visual-restyle.audit.spec.js
// Screenshots land in temp/ui-screenshots (untracked).
const { test, expect } = require('@playwright/test');
const fs = require('fs');
const { login } = require('./helpers/auth');

/**
 * page.screenshot waits for document.fonts in EVERY frame — the cross-origin Turnstile iframe can
 * leave that pending forever in a sandboxed environment. Try the normal screenshot briefly, then
 * fall back to a raw CDP capture (no font wait).
 */
async function robustScreenshot(page, path, fullPage) {
  try {
    await page.screenshot({ path, fullPage, animations: 'disabled', timeout: 8000 });
  } catch {
    const cdp = await page.context().newCDPSession(page);
    const { data } = await cdp.send('Page.captureScreenshot', {
      format: 'png',
      captureBeyondViewport: !!fullPage,
    });
    fs.mkdirSync(require('path').dirname(path), { recursive: true });
    fs.writeFileSync(path, Buffer.from(data, 'base64'));
    await cdp.detach();
  }
}

test.skip(process.env.RUN_VISUAL_AUDIT !== '1', 'visual audit runs only with RUN_VISUAL_AUDIT=1');

const OUT = 'temp/ui-screenshots';
const WIDTHS = [
  { name: 'mobile-360', width: 360, height: 800 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'laptop-1366', width: 1366, height: 768 },
  { name: 'desktop-1920', width: 1920, height: 1080 },
];

// Friendly routes from each page's @page directive — the default /Identity/Account/* routes for
// Login/Register/ForgotPassword are REPLACED by these and no longer resolve.
const ANON_PAGES = [
  { name: 'login', path: '/login' },
  { name: 'register', path: '/register' },
  { name: 'forgot-password', path: '/forgot-password' },
  { name: 'resend-confirmation', path: '/Identity/Account/ResendEmailConfirmation' },
];

const AUTH_PAGES = [
  { name: 'supplier-dashboard', path: '/Dashboard/Dashboard' },
  { name: 'invoices-all', path: '/Invoices/InvoiceLists' },
  { name: 'invoices-draft', path: '/Invoices/InvoiceLists?invoiceDirection=Draft' },
  { name: 'invoices-sent', path: '/Invoices/InvoiceLists?invoiceDirection=Sent' },
  { name: 'invoices-received', path: '/Invoices/InvoiceLists?invoiceDirection=Received' },
];

test.describe('visual audit — restyled pages', () => {
  test('anonymous pages at all breakpoints', async ({ browser }) => {
    test.setTimeout(300000);
    for (const bp of WIDTHS) {
      const ctx = await browser.newContext({ viewport: { width: bp.width, height: bp.height } });
      const page = await ctx.newPage();
      try {
        for (const p of ANON_PAGES) {
          await page.goto(p.path, { waitUntil: 'domcontentloaded', timeout: 30000 });
          await page.waitForTimeout(500);
          const overflow = await page.evaluate(() =>
            document.documentElement.scrollWidth - document.documentElement.clientWidth);
          expect(overflow, `${p.name} horizontal overflow at ${bp.name}`).toBeLessThanOrEqual(17);
          await robustScreenshot(page, `${OUT}/${p.name}-${bp.name}.png`, true);
        }
      } finally {
        await ctx.close();
      }
    }
  });

  test('authenticated pages at all breakpoints (supplier)', async ({ browser }) => {
    test.setTimeout(600000);
    for (const bp of WIDTHS) {
      const ctx = await browser.newContext({ viewport: { width: bp.width, height: bp.height } });
      const page = await ctx.newPage();
      try {
        await login(page, 'supplier');
        for (const p of AUTH_PAGES) {
          await page.goto(p.path, { waitUntil: 'domcontentloaded', timeout: 30000 });
          await page.waitForTimeout(800);
          await robustScreenshot(page, `${OUT}/${p.name}-${bp.name}.png`, false);
        }
      } finally {
        await ctx.close();
      }
    }
  });
});
