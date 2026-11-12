const { test, expect } = require('@playwright/test');
const { watchPage } = require('./helpers/auth');

const PUBLIC_PAGES = [
  { path: '/', name: 'Home' },
  { path: '/About', name: 'About' },
  { path: '/Contact', name: 'Contact' },
  { path: '/OurServices', name: 'Our Services' },
  { path: '/Privacy', name: 'Privacy' },
  { path: '/TermCondition', name: 'Terms & Conditions' },
  { path: '/Resources', name: 'Resources' },
  { path: '/login', name: 'Login' },
  { path: '/register', name: 'Register' },
  { path: '/forgot-password', name: 'Forgot Password' },
  { path: '/Identity/Account/ResendEmailConfirmation', name: 'Resend Confirmation' },
];

for (const pg of PUBLIC_PAGES) {
  test(`public: ${pg.name} (${pg.path}) loads clean`, async ({ page }) => {
    const { consoleErrors, failedRequests } = watchPage(page);
    const resp = await page.goto(pg.path, { waitUntil: 'domcontentloaded' });
    expect(resp.status(), `HTTP status for ${pg.path}`).toBeLessThan(400);
    await expect(page).toHaveTitle(/.+/);
    // Body must render something meaningful
    const text = await page.locator('body').innerText();
    expect(text.trim().length, 'page body has content').toBeGreaterThan(20);
    await page.waitForTimeout(1500); // let async widgets settle
    // Locally, resource-image 404s come from database rows whose image files live on the server's share (absent
    // on a dev box), not from a code defect. On a deployed host (Staging/Production) they are real missing files.
    const local = /^(localhost|127\.0\.0\.1)$/.test(new URL(page.url()).hostname);
    const realFailures = failedRequests.filter(f => !(local && /\/api\/resources\/images\//.test(f)));
    expect.soft(realFailures, `failed same-origin requests on ${pg.path}`).toEqual([]);
    expect(consoleErrors, `same-origin console errors on ${pg.path}`).toEqual([]);
  });
}

test('unknown URL returns friendly 404, not a raw error', async ({ page }) => {
  const resp = await page.goto('/definitely-not-a-real-page-xyz', { waitUntil: 'domcontentloaded' });
  expect([404, 200]).toContain(resp.status());
  const text = (await page.locator('body').innerText()).toLowerCase();
  expect(text).not.toContain('stack trace');
  expect(text).not.toContain('an unhandled exception');
});
