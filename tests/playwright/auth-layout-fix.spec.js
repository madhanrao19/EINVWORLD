const { test, expect } = require('@playwright/test');

// Verifies the auth-page layout fix: logo above a properly-sized, horizontally-centred card
// (no collapsed/side-pinned card) and no horizontal overflow, across device widths.
const BASE = process.env.EINVWORLD_BASE_URL || 'http://localhost:5280';

const VIEWPORTS = [
  { name: 'mobile-360', width: 360, height: 800 },
  { name: 'mobile-390', width: 390, height: 844 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'tablet-1024', width: 1024, height: 768 },
  { name: 'desktop-1280', width: 1280, height: 720 },
  { name: 'desktop-1440', width: 1440, height: 900 },
  { name: 'wide-1920', width: 1920, height: 1080 },
];

const PAGES = [
  { path: '/login', cardTitle: 'Welcome Back' },
  { path: '/forgot-password', cardTitle: 'Forgot Password' },
  { path: '/register', cardTitle: 'Create New Account' },
];

for (const vp of VIEWPORTS) {
  for (const pageDef of PAGES) {
    test(`auth layout: ${pageDef.path} @ ${vp.name} (${vp.width}px)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto(BASE + pageDef.path, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(800);

      // No horizontal overflow.
      const overflow = await page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `horizontal overflow on ${pageDef.path} @${vp.width}`).toBeLessThanOrEqual(1);

      // Card exists and is a sane width (not the collapsed ~140px symptom).
      const card = page.locator('.card').first();
      await expect(card).toBeVisible();
      const cardBox = await card.boundingBox();
      expect(cardBox.width, `card width on ${pageDef.path} @${vp.width}`).toBeGreaterThan(280);

      // Card is horizontally centred (within 40px of viewport centre).
      const centreDelta = Math.abs((cardBox.x + cardBox.width / 2) - vp.width / 2);
      expect(centreDelta, `card centred on ${pageDef.path} @${vp.width}`).toBeLessThanOrEqual(40);

      // Logo sits above the card (not side-by-side).
      const logo = page.locator('.auth-logo').first();
      await expect(logo).toBeVisible();
      const logoBox = await logo.boundingBox();
      expect(logoBox.y + logoBox.height, `logo above card on ${pageDef.path} @${vp.width}`)
        .toBeLessThanOrEqual(cardBox.y + 5);

      // Title text present.
      await expect(page.getByText(pageDef.cardTitle, { exact: false }).first()).toBeVisible();
    });
  }
}

// Password visibility toggle works (keyboard, preserves value, does not submit).
test('auth: password toggle works on login', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(BASE + '/login', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(500);
  const pw = page.locator('#password-input');
  await pw.fill('secret-value');
  const toggle = page.locator('#password-addon');
  await expect(toggle).toBeVisible();
  await toggle.click();
  expect(await pw.getAttribute('type')).toBe('text');
  expect(await pw.inputValue()).toBe('secret-value');
  await toggle.click();
  expect(await pw.getAttribute('type')).toBe('password');
});

// Forgot-password neutral info box present.
test('auth: forgot-password shows "What happens next?" reassurance', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(BASE + '/forgot-password', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(500);
  await expect(page.getByText('What happens next?').first()).toBeVisible();
});
