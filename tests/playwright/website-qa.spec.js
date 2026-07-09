const { test, expect } = require('@playwright/test');
const { watchPage } = require('./helpers/auth');

// Smoke check kept from the original QA scaffold. Same-origin console errors only
// (external analytics/CDN failures are tracked separately, not per-page).
test('homepage loads without same-origin console errors', async ({ page }) => {
  const { consoleErrors } = watchPage(page);
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await expect(page).toHaveTitle(/.+/);
  await page.waitForTimeout(1500);
  expect(consoleErrors).toEqual([]);
});
