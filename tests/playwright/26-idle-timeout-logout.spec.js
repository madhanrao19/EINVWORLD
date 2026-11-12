// Sign-out end to end (v1.50.48): the idle timeout signs out with an antiforgery POST, a GET to the logout URL
// only shows the confirm page, a POST without the token is refused, and the normal Logout button works.
// The idle timeout runs on Playwright's fake clock, so the real IdleTimeoutMinutes setting is used unchanged
// (nothing is shortened). Needs a signed-in test user, so run it against an isolated local app with the Turnstile
// test keys (see helpers/auth.js); it only signs its own session out and changes no data.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const ROLE = process.env.QA_LOGIN_ROLE || 'supplier';

const stillSignedIn = async page => {
  const r = await page.request.get('/Dashboard/Dashboard', { maxRedirects: 0 });
  return r.status() === 200;
};

// Signs in, unless the run already starts signed in (a saved storageState, e.g. a 2FA-enrolled account).
async function signedIn(page) {
  if (await stillSignedIn(page)) return;
  try { await login(page, ROLE); } catch (e) {
    if (process.env.QA_LOGIN_ROLE) throw e; // asked for this role explicitly: a broken sign-in must fail, not skip
    test.skip(true, `${ROLE} login unavailable (needs its E2E_*_PASSWORD and the Turnstile test key)`);
  }
}

test('idle timeout warns, then signs out with a POST and shows the inactivity message', async ({ page }) => {
  test.setTimeout(120000);
  await signedIn(page);
  await page.clock.install(); // before the page's scripts start their timers
  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });

  const minutes = await page.evaluate(() => idleTimeoutMinutes); // the real configured value
  expect(minutes).toBeGreaterThan(1);

  const logoutRequests = [];
  page.on('request', r => { if (/\/Identity\/Account\/Logout/i.test(r.url())) logoutRequests.push(r); });

  // No input events from here on: any mousemove/keydown would restart the timers.
  await page.clock.fastForward((minutes - 1) * 60000 + 1000);
  await expect(page.getByText('Session Expiring')).toBeVisible();
  expect(await stillSignedIn(page)).toBe(true);

  await Promise.all([
    page.waitForURL(/\/login\?logoutReason=timeout/i, { waitUntil: 'commit' }),
    page.clock.fastForward(60000),
  ]);
  expect(logoutRequests.map(r => r.method())).toEqual(['POST']);
  expect(logoutRequests[0].postData() || '').toMatch(/__RequestVerificationToken=[^&]{20,}/);
  expect(await stillSignedIn(page)).toBe(false);
});

test('logout GET only confirms, a POST without the token is refused, the Logout button signs out', async ({ page }) => {
  test.setTimeout(90000);
  await signedIn(page);

  const get = await page.goto('/Identity/Account/Logout', { waitUntil: 'domcontentloaded' });
  expect(get.status()).toBe(200);
  expect(await stillSignedIn(page)).toBe(true);

  // What another site could send: the user's cookies but no antiforgery token.
  const forged = await page.request.post('/Identity/Account/Logout', { maxRedirects: 0, form: { logoutReason: 'forged' } });
  expect(forged.status()).toBe(400);
  expect(await stillSignedIn(page)).toBe(true);

  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
  const button = page.locator('form[action*="Account/Logout"][method="post"] button[type="submit"]').first();
  await expect(button).toHaveCount(1);
  await Promise.all([
    page.waitForURL(/\/login/i, { waitUntil: 'commit' }),
    button.evaluate(b => b.closest('form').requestSubmit(b)), // the sidebar/menu button may be inside a closed dropdown
  ]);
  expect(await stillSignedIn(page)).toBe(false);
});
