const { test, expect } = require('@playwright/test');
const { login, watchPage } = require('./helpers/auth');

test.describe.configure({ mode: 'serial' });

// Admin has 2FA enrolled in staging (can't be driven password-only), so authenticated
// crawls run as Supplier and Buyer — they exercise the same shared layout/nav/dashboard
// components. Admin's access control is covered by 02-auth (2FA challenge) and 03-authz.
for (const role of ['supplier', 'buyer']) {
  test(`dashboard: ${role} landing renders without errors`, async ({ page }) => {
    const { consoleErrors, failedRequests } = watchPage(page);
    await login(page, role);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    const server500 = failedRequests.filter(f => /^5\d\d /.test(f));
    expect.soft(failedRequests, `failed requests on ${role} landing`).toEqual([]);
    expect(server500, `5xx on ${role} landing`).toEqual([]);
    expect(consoleErrors, `console errors on ${role} landing`).toEqual([]);
  });

  test(`nav crawl: every menu link works for ${role}`, async ({ page }) => {
    test.setTimeout(300000);
    await login(page, role);
    await page.waitForLoadState('domcontentloaded');
    const hrefs = await page.$$eval('a[href]', as => as.map(a => a.getAttribute('href')));
    const base = new URL(page.url());
    const targets = [...new Set(hrefs
      .filter(h => h && !h.startsWith('#') && !h.startsWith('javascript') && !h.startsWith('mailto') && !h.startsWith('tel'))
      .map(h => new URL(h, base).toString())
      .filter(u => new URL(u).host === base.host)
      // Never GET-crawl links that could mutate/destroy data or end the session.
      .filter(u => !/logout|signout|delete|remove|cancel|reject|submit|resend|approve|download|export|\.pdf/i.test(u)))];
    const broken = [];
    for (const url of targets) {
      const resp = await page.goto(url, { waitUntil: 'domcontentloaded' }).catch(e => ({ status: () => `NAV-ERROR ${e.message}` }));
      const status = resp ? resp.status() : 'no-response';
      if (typeof status !== 'number' || status >= 500) broken.push(`${status} ${url}`);
      else if (status >= 400) broken.push(`${status} ${url}`);
    }
    expect(broken, `broken links for ${role}`).toEqual([]);
  });
}
