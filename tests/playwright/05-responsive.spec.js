const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const VIEWPORTS = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1440, height: 900 },
];

const PAGES = ['/', '/Contact', '/login', '/register', '/Resources'];

for (const vp of VIEWPORTS) {
  for (const path of PAGES) {
    test(`responsive: ${path} has no horizontal overflow at ${vp.name} (${vp.width}px)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto(path, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(1500);
      const overflow = await page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `horizontal overflow px on ${path} @${vp.width}`).toBeLessThanOrEqual(1);
    });
  }
}

test('responsive: supplier dashboard usable at mobile width', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await login(page, 'supplier');
  await page.waitForTimeout(1500);
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
});

test('pwa: manifest is installable and theme-color matches the brand', async ({ page, request }) => {
  const resp = await request.get('/favicon/manifest.json');
  expect(resp.status()).toBe(200);
  const m = await resp.json();
  expect(m.id).toBe('/');
  expect(m.scope).toBe('/');
  expect(m.start_url).toBe('/Dashboard/Dashboard');
  expect(m.display).toBe('standalone');
  const sizes = m.icons.map(i => i.sizes);
  expect(sizes).toEqual(expect.arrayContaining(['192x192', '512x512']));
  // Every icon must actually be served.
  for (const icon of m.icons) expect((await request.get(icon.src)).status(), icon.src).toBe(200);

  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('link[rel="manifest"]')).toHaveCount(1);
  await expect(page.locator('meta[name="theme-color"][media*="light"]')).toHaveAttribute('content', '#006948');
  // No service worker by design (no cached tenant pages on shared devices).
  expect(await page.evaluate(async () => (await navigator.serviceWorker?.getRegistrations?.() ?? []).length)).toBe(0);
});
