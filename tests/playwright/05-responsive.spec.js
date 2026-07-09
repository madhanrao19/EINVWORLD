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
