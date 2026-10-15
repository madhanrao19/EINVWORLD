// Ledger Phase 4 (v1.46.0): signed-in pages use only the Tabler shell — no Velzon app CSS, Velzon icon
// bundle, Font Awesome or lord-icon requests — and icons still render (Remix font loaded). Public pages keep
// the marketing layout whether or not you're signed in, and offer "Dashboard" instead of "Login" when signed in.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const VELZON = /\/assets\/css\/(app|icons|bootstrap|custom)\.min\.css|font-awesome|lord-icon|\/assets\/js\/app\.js/;

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

test('signed-in pages load no Velzon assets and icons render', async ({ page }) => {
  test.setTimeout(120000);
  await loginOrSkip(page, 'supplier');
  const hits = [];
  page.on('request', r => { if (VELZON.test(r.url())) hits.push(new URL(r.url()).pathname); });
  for (const url of ['/Dashboard/Dashboard', '/Invoices/InvoiceLists', '/Invoices/CreateInvoice', '/Profile/Index', '/Error']) {
    await page.goto(url, { waitUntil: 'load' });
    await expect(page.locator('aside.navbar-vertical')).toBeVisible(); // Tabler shell, including the error page
  }
  expect(hits, 'Velzon / Font Awesome / lord-icon assets requested on signed-in pages').toEqual([]);
  // Remix icons draw with their own font (a missing font would fall back to the page font).
  const family = await page.evaluate(() => getComputedStyle(document.querySelector('.navbar-vertical [class^="ri-"], .navbar-vertical [class*=" ri-"]'), '::before').fontFamily);
  expect(family).toMatch(/remixicon/i);
});

test('public pages keep the marketing layout; signed-in visitors see Dashboard, not Login', async ({ page }) => {
  test.setTimeout(90000);
  await page.goto('/contact', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('aside.navbar-vertical')).toHaveCount(0);
  await expect(page.locator('nav').getByRole('link', { name: 'Login' })).toBeVisible();

  await loginOrSkip(page, 'supplier');
  await page.goto('/contact', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('aside.navbar-vertical')).toHaveCount(0); // not squeezed into the app shell
  await expect(page.locator('nav').getByRole('link', { name: 'Dashboard' })).toBeVisible();
  await expect(page.locator('nav').getByRole('link', { name: 'Login' })).toHaveCount(0);
});
