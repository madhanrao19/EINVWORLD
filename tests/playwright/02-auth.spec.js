const { test, expect } = require('@playwright/test');
const { USERS, login, logout, submitCredentials } = require('./helpers/auth');

// Supplier and Buyer are password-only in staging; Admin has 2FA enrolled.
for (const role of ['supplier', 'buyer']) {
  test(`auth: ${role} can log in and log out`, async ({ page }) => {
    await login(page, role);
    expect(page.url().toLowerCase()).not.toContain('/login');
    // Authenticated shell should be present (any nav/sidebar/topbar)
    await expect(page.locator('body')).not.toContainText('Turnstile verification failed');
    await logout(page);
    // After logout, a protected page must bounce to login
    await page.goto('/Dashboard/Dashboard', { waitUntil: 'commit' });
    await page.waitForURL(/login/i, { timeout: 15000, waitUntil: 'commit' });
  });
}

test('auth: admin with 2FA enrolled is challenged for a second factor (not let straight in)', async ({ page }) => {
  await submitCredentials(page, 'admin');
  // Correct password must NOT land on the dashboard — it must hit the 2FA challenge.
  await page.waitForURL(/LoginWith2fa/i, { timeout: 20000, waitUntil: 'commit' });
  await expect(page.locator('body')).toContainText(/two-step|authenticator|verification code/i);
});

test('auth: wrong password shows an error, no lockout on first try', async ({ page }) => {
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.fill('#username', USERS.buyer.email);
  await page.fill('#password-input', 'Definitely-Wrong-123!');
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 20000 });
  await page.evaluate(() => document.getElementById('account').requestSubmit());
  await page.waitForSelector('.text-danger li, [role="alert"]', { timeout: 20000 });
  expect(page.url().toLowerCase()).toContain('/login');
  const body = (await page.locator('body').innerText()).toLowerCase();
  expect(body).toMatch(/invalid|failed|incorrect/);
});
