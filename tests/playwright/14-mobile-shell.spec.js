// Mobile shell checks for the authenticated Tabler layout (Supplier + Buyer), on a touch phone:
//   - the hamburger opens/closes the offcanvas nav drawer
//   - the topbar search is reachable via its own toggler (it is hidden inline below md)
//   - the compact shell controls meet the 44px touch-target minimum (coarse pointer)
//   - the Supplier company-workspace pages (reached via the "My Company" nav link, so no
//     hard-coded company id) have no horizontal overflow at phone/tablet widths
// Read-only: browses only, never saves or submits. Same prerequisites as 10-tabler-modules
// (Turnstile TEST keys so login() passes; skipped if a role's login is 2FA-gated).

const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

test.use({ viewport: { width: 375, height: 800 }, hasTouch: true, isMobile: true });

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

async function overflowPx(page) {
  return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
}

for (const role of ['supplier', 'buyer']) {
  test(`${role}: mobile drawer, search toggle and touch targets`, async ({ page }) => {
    test.setTimeout(90000);
    await loginOrSkip(page, role);
    await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });

    // Drawer: closed → open → a nav link is visible → close.
    const drawer = page.locator('aside.navbar-vertical .offcanvas-lg');
    await page.locator('aside.navbar-vertical .navbar-toggler').click();
    await expect(drawer).toHaveClass(/\bshow\b/);
    await expect(drawer.locator('a.nav-link[href^="/"]').first()).toBeVisible();
    await drawer.locator('[data-bs-dismiss="offcanvas"]').first().click();
    await expect(drawer).not.toHaveClass(/\bshow\b/);

    // Search: hidden until its toggler is tapped, then visible and focused.
    const search = page.locator('#einv-search-input');
    await expect(search).toBeHidden();
    await page.locator('#einv-search-toggle').click();
    await expect(search).toBeVisible();
    await expect(search).toBeFocused();
    await expect(page.locator('#einv-search-toggle')).toHaveAttribute('aria-expanded', 'true');

    // Touch targets on the shell controls.
    for (const sel of ['#einv-search-toggle', '#einv-theme-toggle', 'aside.navbar-vertical .navbar-toggler']) {
      const box = await page.locator(sel).boundingBox();
      expect(box, `${sel} rendered`).not.toBeNull();
      expect(Math.round(box.width), `${sel} width`).toBeGreaterThanOrEqual(44);
      expect(Math.round(box.height), `${sel} height`).toBeGreaterThanOrEqual(44);
    }

    expect(await overflowPx(page), 'dashboard horizontal overflow at 375px').toBeLessThanOrEqual(1);
  });
}

test('supplier: company workspace pages have no horizontal overflow', async ({ page }) => {
  test.setTimeout(120000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
  const details = await page.locator('aside.navbar-vertical a[href^="/Suppliers/Details?id="]').first().getAttribute('href');
  expect(details, '"My Company" nav link').toBeTruthy();
  const id = new URL(details, 'http://x').searchParams.get('id');
  const pages = ['Details', 'Edit', 'Users', 'Security', 'Audit', 'RolesPermissions', 'InvoiceBranding']
    .map(p => `/Suppliers/${p}?id=${id}`);
  for (const width of [375, 768]) {
    await page.setViewportSize({ width, height: 900 });
    for (const p of pages) {
      const resp = await page.goto(p, { waitUntil: 'domcontentloaded' });
      expect(resp && resp.status(), `${p} HTTP status`).toBeLessThan(400);
      expect(await overflowPx(page), `${p} horizontal overflow at ${width}px`).toBeLessThanOrEqual(1);
    }
  }
});

// Dark mode must survive navigation: the server renders data-bs-theme from the einv-theme cookie.
// Regression: the attribute was built as an encoded string, so pages rendered data-bs-theme="&quot;dark&quot;"
// (the value "dark" in quotes), which no [data-bs-theme=dark] rule matches, and every page came back light.
// Also checks each role's pages carry the PWA manifest + theme-color (installable for Admin too).
for (const role of ['supplier', 'buyer', 'admin']) {
  test(`${role}: dark mode persists across pages, and pages are installable`, async ({ page, context }) => {
    test.setTimeout(120000);
    await page.setViewportSize({ width: 1366, height: 800 });
    await loginOrSkip(page, role);
    await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
    test.skip(/login|2fa/i.test(page.url()), `login for '${role}' is 2FA-gated`);
    try {
      if (await page.locator('html').getAttribute('data-bs-theme') !== 'dark') await page.click('#einv-theme-toggle');
      await expect(page.locator('html')).toHaveAttribute('data-bs-theme', 'dark');
      // The toggle repaints the browser/status bar at once (installed app), whatever the OS theme.
      for (const meta of await page.locator('meta[name="theme-color"]').all()) {
        await expect(meta).toHaveAttribute('content', '#0c1411');
      }

      const links = (await page.$$eval('aside.navbar-vertical a[href^="/"]', as => [...new Set(as.map(a => a.getAttribute('href')))]))
        .filter(h => !/logout/i.test(h)).slice(0, 6);
      for (const href of links) {
        await page.goto(href, { waitUntil: 'domcontentloaded' });
        await expect(page.locator('html'), `theme on ${href}`).toHaveAttribute('data-bs-theme', 'dark');
        await expect(page.locator('link[rel="manifest"]'), `manifest on ${href}`).toHaveCount(1);
        // A saved theme renders one theme-color in that theme's colour, not the OS-dependent pair.
        await expect(page.locator('meta[name="theme-color"]'), `theme-color on ${href}`).toHaveCount(1);
        await expect(page.locator('meta[name="theme-color"]'), `theme-color on ${href}`).toHaveAttribute('content', '#0c1411');
      }
    } finally {
      // Leave the shared demo account's browser state as found (cookie is per test context anyway).
      await context.clearCookies({ name: 'einv-theme' });
    }
  });
}

// Invoice Details shows the invoice like a browser PDF viewer: a fixed A4 sheet zoomed to fit the
// screen (never reflowed), -/+/fit zoom, print at 100%, and the sheet stays white "paper" in dark mode.
test('supplier: invoice preview behaves like a PDF viewer (phone, print, dark mode)', async ({ page, context }) => {
  test.setTimeout(120000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });
  const href = await page.locator('#invoiceTable a[href*="InvoiceDetails2"]').first().getAttribute('href').catch(() => null);
  test.skip(!href, 'no sent invoice to open');

  await page.goto(href, { waitUntil: 'domcontentloaded' });
  const sheet = page.locator('#einvPdfPage');
  const level = page.locator('#einvPdfZoomLevel');
  await expect(sheet).toBeVisible();

  // Fit to width on a 375px phone: zoomed below 100%, the sheet keeps its A4 proportions, no page overflow.
  const fitPct = parseInt(await level.textContent(), 10);
  expect(fitPct).toBeLessThan(100);
  const box = await sheet.boundingBox();
  expect(box.height / box.width, 'A4 aspect ratio').toBeGreaterThan(1.35);
  expect(await overflowPx(page), 'page overflow at 375px').toBeLessThanOrEqual(1);

  await page.click('#einvPdfZoomIn');
  await expect(level).toHaveText(`${fitPct + 10}%`);
  await expect(page.locator('#einvPdfFit')).toHaveAttribute('aria-pressed', 'false');
  await page.click('#einvPdfFit');
  await expect(level).toHaveText(`${fitPct}%`);
  expect(await overflowPx(page), 'zooming scrolls inside the viewer, not the page').toBeLessThanOrEqual(1);

  // Print renders the sheet at 100% with no viewer chrome.
  await page.emulateMedia({ media: 'print' });
  expect(await sheet.evaluate(el => getComputedStyle(el).zoom)).toBe('1');
  await expect(page.locator('.einv-pdf-toolbar')).toBeHidden();
  await page.emulateMedia({ media: 'screen' });

  // Dark mode: the sheet is still black-on-white paper; app links use the lighter dark-mode green.
  await context.addCookies([{ name: 'einv-theme', value: 'dark', url: new URL(page.url()).origin }]);
  await page.reload({ waitUntil: 'domcontentloaded' });
  const paper = await sheet.evaluate(el => ({ bg: getComputedStyle(el).backgroundColor, fg: getComputedStyle(el).color }));
  expect(paper).toEqual({ bg: 'rgb(255, 255, 255)', fg: 'rgb(0, 0, 0)' });
  const link = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--tblr-link-color').trim());
  expect(link).toBe('#34d399');
});

// Dark mode on the Create Invoice wizard: Invoice Items rows and Review & Submit summary used
// light-only page styles (white rows, #3d4a42 text) that were unreadable on the dark canvas.
test('supplier: Create Invoice steps are readable in dark mode', async ({ page, context }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await context.addCookies([{ name: 'einv-theme', value: 'dark', url: new URL(page.url()).origin }]);
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => ['step2', 'step3'].forEach(id => document.getElementById(id).classList.remove('d-none')));

  const lum = c => {
    const [r, g, b] = c.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number).map(v => { v /= 255; return v <= .03928 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; });
    return .2126 * r + .7152 * g + .0722 * b;
  };
  const rowBg = await page.locator('.item-row').first().evaluate(el => getComputedStyle(el).backgroundColor);
  expect(lum(rowBg), `item row background ${rowBg}`).toBeLessThan(.1);
  const summary = await page.locator('.ci-summary-row').first().evaluate(el => getComputedStyle(el).color);
  expect(lum(summary), `review summary text ${summary}`).toBeGreaterThan(.5);
  const step = await page.locator('.ci-step-node:not(.is-active) .ci-step-circle').first().evaluate(el => getComputedStyle(el).backgroundColor);
  expect(lum(step), `inactive step circle ${step}`).toBeLessThan(.1);
});

// Security regression: dashboard.js builds its insight panels with innerHTML. Company/staff names,
// invoice numbers and LHDN reasons are user/server data and must render as text, never as markup.
test('supplier: dashboard insight panels escape names and reasons (no XSS)', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  const PROBE = '<img src=x id="xss-probe" onerror="window.__xss=1">';
  await page.route(/handler=InvoiceData/, async route => {
    const resp = await route.fetch();
    const data = await resp.json();
    data.internalRejectedReasons = [{ reason: PROBE, suggestedFix: PROBE, count: 1 }];
    data.customerRejectionRates = [{ customerName: PROBE, rejectionRate: 60, rejectedCount: 3, totalInvoices: 5 }];
    data.internalErrorRates = [{ staffName: PROBE, errorRate: 25, cancelledCount: 1, totalCreated: 4 }];
    data.agingDrafts = [{ invoiceNo: PROBE, customerName: PROBE, createdDate: PROBE, daysStuck: 9 }];
    await route.fulfill({ response: resp, json: data });
  });
  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#buyerRejectionTable')).toContainText('<img src=x', { timeout: 20000 });
  await expect(page.locator('#xss-probe')).toHaveCount(0);
  expect(await page.evaluate(() => window.__xss)).toBeUndefined();
});
