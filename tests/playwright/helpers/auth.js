const { expect } = require('@playwright/test');

const USERS = {
  admin: { email: 'admin@einvworld.com', password: 'Admin@123' },
  supplier: { email: 'supplier@einvworld.com', password: 'Supplier@123' },
  buyer: { email: 'buyer@einvworld.com', password: 'Buyer@123' },
};

/**
 * Fills and submits the login form (does NOT assert where it lands). Requires the app to
 * run with the Cloudflare Turnstile test site key (1x00000000000000000000AA) so the widget
 * auto-passes. Used both for full login and for asserting the 2FA challenge.
 */
async function submitCredentials(page, role) {
  const user = USERS[role];
  if (!user) throw new Error(`Unknown role: ${role}`);
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.fill('#username', user.email);
  await page.fill('#password-input', user.password);
  // Wait for the Turnstile test widget to inject its token before submitting.
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 20000 });
  await page.evaluate(() => document.getElementById('account').requestSubmit());
}

/**
 * Logs a password-only user fully in (no 2FA). Requires the Turnstile test site key.
 */
async function login(page, role) {
  const user = USERS[role];
  if (!user) throw new Error(`Unknown role: ${role}`);
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.fill('#username', user.email);
  await page.fill('#password-input', user.password);
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 20000 });
  await Promise.all([
    // 'commit' (not the default 'load') because a blocked external CDN/analytics host can
    // stall the window 'load' event for ~20s even after the app HTML has fully arrived.
    page.waitForURL(url => !/\/login\/?$/i.test(url.pathname), { timeout: 20000, waitUntil: 'commit' }),
    // Submit the form directly (not a button click) to avoid the waves-effect button's
    // stability wait flaking under animation/turnstile layout shifts.
    page.evaluate(() => document.getElementById('account').requestSubmit()),
  ]);
  // Confirm we reached an authenticated shell, not a re-rendered login/2fa page.
  await page.waitForSelector('#login-submit', { state: 'detached', timeout: 20000 });
}

async function logout(page) {
  await page.goto('/Identity/Account/Logout', { waitUntil: 'domcontentloaded' });
  const btn = page.locator('form button[type="submit"], form input[type="submit"]').first();
  if (await btn.count()) await btn.click({ timeout: 10000 }).catch(() => {});
}

const isLocal = host => host.startsWith('localhost') || host.startsWith('127.0.0.1');

/**
 * Attaches collectors that flag ONLY same-origin (localhost) problems. External
 * CDN / analytics failures (e.g. blocked googletagmanager.com in a sandbox) are
 * ignored here — they are tracked separately as a design finding, not per-page.
 */
function watchPage(page) {
  const consoleErrors = [];   // same-origin JS/resource errors only
  const externalErrors = [];  // third-party failures, informational
  const failedRequests = [];  // same-origin HTTP >= 400 / network failures
  page.on('console', msg => {
    if (msg.type() !== 'error') return;
    const text = msg.text();
    // Generic "Failed to load resource" messages carry no reliable origin; same-origin
    // resource failures are already captured by the response/requestfailed handlers below,
    // so treat these as external noise here to avoid double-counting blocked CDNs.
    if (/Failed to load resource/i.test(text)) { externalErrors.push(text); return; }
    const loc = msg.location && msg.location();
    let host = '';
    try { host = loc && loc.url ? new URL(loc.url).host : ''; } catch { /* ignore */ }
    if (host && !isLocal(host)) externalErrors.push(text);
    else consoleErrors.push(text);
  });
  page.on('response', resp => {
    try {
      const u = new URL(resp.url());
      if (isLocal(u.host) && resp.status() >= 400) failedRequests.push(`${resp.status()} ${u.pathname}`);
    } catch { /* ignore */ }
  });
  page.on('requestfailed', req => {
    try {
      const u = new URL(req.url());
      if (isLocal(u.host)) failedRequests.push(`FAILED ${u.pathname} (${req.failure()?.errorText})`);
      else externalErrors.push(`external failed: ${u.host}${u.pathname}`);
    } catch { /* ignore */ }
  });
  return { consoleErrors, externalErrors, failedRequests };
}

module.exports = { USERS, login, logout, submitCredentials, watchPage };
