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
