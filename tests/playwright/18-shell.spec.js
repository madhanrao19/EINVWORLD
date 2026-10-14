// App shell (Ledger Phase 3b): one top bar on phones, a usable menu drawer, visible close buttons
// in dark mode, and a collapsed desktop sidebar that actually gives the page its width back.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 800 }, hasTouch: true, isMobile: true });

  test('one top bar, drawer fills its panel, close button visible in dark', async ({ page, context, baseURL }) => {
    test.setTimeout(90000);
    await context.addCookies([{ name: 'einv-theme', value: 'dark', url: baseURL }]);
    await loginOrSkip(page, 'supplier');
    await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });

    // Theme toggle sits on the same row as the menu button (was a second 56px row).
    const menuBox = await page.locator('aside.navbar-vertical .navbar-toggler').boundingBox();
    const themeBox = await page.locator('#einv-theme-toggle').boundingBox();
    expect(Math.abs((themeBox.y + themeBox.height / 2) - (menuBox.y + menuBox.height / 2))).toBeLessThan(8);

    await page.locator('aside.navbar-vertical .navbar-toggler').click();
    const drawer = page.locator('#einv-sidebar-menu');
    await expect(drawer).toHaveClass(/\bshow\b/);
    const d = await drawer.boundingBox();
    expect(d.width, 'drawer leaves the backdrop tappable').toBeLessThan(390);
    const nav = await drawer.locator('nav').boundingBox();
    expect(nav.width, 'menu fills the drawer (was a centred 194px column)').toBeGreaterThan(d.width * 0.8);

    // Tabler draws the "x" with a currentColor mask; it must not be painted transparent.
    const close = drawer.locator('.btn-close');
    await expect(close).toBeVisible();
    const bg = await close.evaluate(el => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe('rgba(0, 0, 0, 0)');
    await close.click();
    await expect(drawer).not.toHaveClass(/\bshow\b/);

    // Scrolled to the end, the Dashboard's floating "+" must not cover the footer's Contact link.
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    const contact = await page.locator('footer a[href="/contact"]').boundingBox();
    const fab = await page.locator('.einv-fab').boundingBox();
    expect(contact.y + contact.height <= fab.y || fab.x + fab.width <= contact.x || contact.x + contact.width <= fab.x,
      'footer Contact link clear of the floating button').toBe(true);
  });
});

test('desktop: collapsed sidebar hands its width to the page', async ({ page }) => {
  test.setTimeout(90000);
  await page.setViewportSize({ width: 1366, height: 800 });
  await loginOrSkip(page, 'supplier');
  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
  const wrapper = page.locator('.page-wrapper');
  const toggle = page.locator('#einv-sidebar-collapse-toggle');
  // Headless Chrome uses overlay scrollbars, so assert the cause of the classic-scrollbar strip directly.
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).marginLeft)).toBe('0px');
  try {
    await toggle.click();
    await expect(page.locator('body')).toHaveClass(/einv-sidebar-collapsed/);
    const aside = await page.locator('aside.navbar-vertical').boundingBox();
    const main = await wrapper.boundingBox();
    expect(Math.round(main.x)).toBe(Math.round(aside.width));
    await expect(page.locator('.einv-nav-label').first()).toBeHidden();
  } finally {
    // The collapsed state persists in localStorage; leave the browser as we found it.
    if (await page.locator('body.einv-sidebar-collapsed').count()) await toggle.click();
  }
  await expect(page.locator('.einv-nav-label').first()).toBeVisible();
});
