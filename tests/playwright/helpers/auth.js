const { expect } = require('@playwright/test');
const crypto = require('crypto');

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
  await page.goto('/login', { waitUntil: 'commit', timeout: 60000 });
  await page.fill('#username', user.email);
  await page.fill('#password-input', user.password);
  // Wait for the Turnstile test widget to inject its token before submitting.
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 35000 });
  await page.evaluate(() => document.getElementById('account').requestSubmit());
}

/**
 * Logs a password-only user fully in (no 2FA). Requires the Turnstile test site key.
 */
async function login(page, role) {
  const user = USERS[role];
  if (!user) throw new Error(`Unknown role: ${role}`);
  await page.goto('/login', { waitUntil: 'commit', timeout: 60000 });
  await page.fill('#username', user.email);
  await page.fill('#password-input', user.password);
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 35000 });
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

function base32Decode(s) {
  const A = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  s = s.replace(/[\s-]+/g, '').toUpperCase().replace(/=+$/, '');
  let bits = '';
  for (const c of s) {
    const v = A.indexOf(c);
    if (v < 0) throw new Error(`Invalid base32 char: ${c}`);
    bits += v.toString(2).padStart(5, '0');
  }
  const bytes = [];
  for (let i = 0; i + 8 <= bits.length; i += 8) bytes.push(parseInt(bits.slice(i, i + 8), 2));
  return Buffer.from(bytes);
}

function totp(sharedKey, atMs = Date.now()) {
  const key = base32Decode(sharedKey);
  const counter = Math.floor(atMs / 1000 / 30);
  const buf = Buffer.alloc(8);
  buf.writeBigUInt64BE(BigInt(counter));
  const h = crypto.createHmac('sha1', key).update(buf).digest();
  const o = h[h.length - 1] & 0xf;
  const code = (((h[o] & 0x7f) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3]) % 1e6;
  return String(code).padStart(6, '0');
}

/** Waits until at least `min` seconds remain in the current 30s TOTP window, then returns a code. */
async function freshTotp(sharedKey, min = 5) {
  const secondsLeft = 30 - (Math.floor(Date.now() / 1000) % 30);
  if (secondsLeft < min) await new Promise(r => setTimeout(r, (secondsLeft + 1) * 1000));
  return totp(sharedKey);
}

/** Logs in a 2FA-enrolled user, answering the authenticator challenge from its shared key. */
async function loginWithTotp(page, role, sharedKey) {
  await submitCredentials(page, role);
  await page.waitForURL(/LoginWith2fa/i, { timeout: 20000 });
  await page.fill('#Input_TwoFactorCode', await freshTotp(sharedKey));
  await Promise.all([
    page.waitForURL(u => !/LoginWith2fa|\/login\/?$/i.test(u.pathname + u.search), { timeout: 20000 }),
    page.locator('form button[type="submit"], form input[type="submit"]').first().click(),
  ]);
}

module.exports = { USERS, login, logout, submitCredentials, watchPage, totp, freshTotp, loginWithTotp };
