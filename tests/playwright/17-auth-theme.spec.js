// Auth pages (Ledger Phase 3a): they follow the saved theme (einv-theme cookie) like the app, the
// primary button keeps AA white text in dark mode (fill green, not the light text green), the logo
// swaps to the light variant, Turnstile matches the page, and signed-in auth pages render in
// standards mode (the idle-timeout script used to be emitted before <!DOCTYPE>).
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const cssColor = (page, varName) => page.evaluate(v => {
  const probe = document.createElement('div');
  probe.style.color = getComputedStyle(document.documentElement).getPropertyValue(v);
  document.body.appendChild(probe);
  const c = getComputedStyle(probe).color;
  probe.remove();
  return c;
}, varName);

for (const theme of ['light', 'dark']) {
  test(`login page renders in the saved ${theme} theme`, async ({ page, context, baseURL }) => {
    await context.addCookies([{ name: 'einv-theme', value: theme, url: baseURL }]);
    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme);
    expect(await page.evaluate(() => document.compatMode)).toBe('CSS1Compat');

    const fill = await cssColor(page, '--einv-fill');
    const btnBg = await page.locator('#login-submit').evaluate(el => getComputedStyle(el).backgroundColor);
    expect(btnBg).toBe(fill);

    const visibleLogo = theme === 'dark' ? '.einv-auth-form-logo .einv-logo-on-dark' : '.einv-auth-form-logo .einv-logo-on-light';
    await expect(page.locator(visibleLogo)).toBeVisible();
    await expect(page.locator('.cf-turnstile')).toHaveAttribute('data-theme', theme);
  });
}

test('login page without a saved theme follows the OS setting', async ({ browser }) => {
  const ctx = await browser.newContext({ colorScheme: 'dark' });
  const page = await ctx.newPage();
  try {
    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', 'dark');
    await expect(page.locator('.cf-turnstile')).toHaveAttribute('data-theme', 'auto');
  } finally {
    await ctx.close();
  }
});

test('signed-in account pages render in standards mode', async ({ page }) => {
  await login(page, 'supplier');
  await page.goto('/Identity/Account/Manage/ChangePassword', { waitUntil: 'domcontentloaded' });
  test.skip(/login/i.test(page.url()), 'account page redirected to login');
  expect(await page.evaluate(() => document.compatMode)).toBe('CSS1Compat');
  // The idle-timeout timer is still installed for signed-in users.
  expect(await page.evaluate(() => typeof idleTimeoutMinutes)).toBe('number');
});
