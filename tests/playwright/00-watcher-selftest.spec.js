const { test, expect } = require('@playwright/test');
const { watchPage } = require('./helpers/auth');

// Self-test for helpers/auth.js watchPage: it must catch the app's own errors on whatever host the suite runs
// against (localhost, Staging, Production), not only localhost. Isolated: every request is answered by
// page.route inside the browser, so nothing reaches a real server even when EINVWORLD_BASE_URL is Staging.
const base = new URL(process.env.EINVWORLD_BASE_URL || 'http://localhost:5210');
const appUrl = path => new URL(path, base).toString();

test('watchPage catches the app\'s own errors and leaves third-party noise out', async ({ page }) => {
  const watch = watchPage(page);

  await page.route('**/*', route => {
    const url = new URL(route.request().url());
    if (url.host === 'third-party.example') {
      return route.fulfill({ contentType: 'text/javascript', body: 'console.error("qa-third-party-error"); throw new Error("qa-third-party-throw");' });
    }
    if (url.host !== base.host) return route.abort();
    switch (url.pathname) {
      case '/__qa-watcher-selftest':
        return route.fulfill({
          contentType: 'text/html',
          // Report-only CSP that the inline scripts below violate (as Staging's CSP-RO does): must not fail the watcher.
          headers: { 'Content-Security-Policy-Report-Only': "script-src 'self' https://third-party.example" },
          body: `<!doctype html><html><body>
            <script>console.error('qa-intentional-console-error');</script>
            <script src="/__qa-app.js"></script>
            <script src="/cdn-cgi/qa-edge.js"></script>
            <script src="https://third-party.example/x.js"></script>
            <script>fetch('/__qa-500');</script>
          </body></html>`,
        });
      case '/__qa-app.js':
        return route.fulfill({ contentType: 'text/javascript', body: 'throw new Error("qa-intentional-app-throw");' });
      case '/cdn-cgi/qa-edge.js':
        return route.fulfill({ contentType: 'text/javascript', body: 'console.error("qa-cloudflare-edge-error");' });
      case '/__qa-500':
        return route.fulfill({ status: 500, body: 'boom' });
      default:
        return route.fulfill({ status: 404, body: '' });
    }
  });

  await page.goto(appUrl('/__qa-watcher-selftest'), { waitUntil: 'load' });
  await expect.poll(() => watch.failedRequests.length).toBeGreaterThan(0);
  await page.waitForTimeout(300);

  const app = watch.consoleErrors.join('\n');
  const external = watch.externalErrors.join('\n');
  // Caught: the app's own console.error (inline), uncaught exception from an app script, and an app 500.
  expect(app).toContain('qa-intentional-console-error');
  expect(app).toContain('qa-intentional-app-throw');
  expect(watch.failedRequests).toContain('500 /__qa-500');
  // Not counted as app errors: third-party script, Cloudflare edge (/cdn-cgi/), CSP report-only notices.
  expect(app).not.toContain('qa-third-party');
  expect(app).not.toContain('qa-cloudflare-edge-error');
  expect(external).toContain('qa-third-party-error');
  expect(external).toContain('qa-third-party-throw');
  expect(external).toContain('qa-cloudflare-edge-error');
  expect(app).not.toMatch(/\[Report Only\]/);
});
