// @ts-check
// End-to-end proof that the 2FA "Don't ask again on this device" checkbox works.
//
// Self-contained and self-cleaning: enrolls the BUYER QA account in authenticator 2FA
// (computing real TOTP codes from the shared key the enrollment page displays), verifies
// the remember-machine cookie skips the second factor on the next login on the SAME
// browser context, verifies a FRESH context is still challenged (device-bound), then
// disables 2FA + resets the authenticator so the account returns to its original state.
//
// Run explicitly: npx playwright test 09-2fa-remember-device
const { test, expect } = require('@playwright/test');
const crypto = require('crypto');
const { USERS, submitCredentials, logout } = require('./helpers/auth');

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

test.describe.configure({ mode: 'serial' });
test.setTimeout(180000);

test('2FA "Don\'t ask again on this device" remembers the device and a fresh device is still challenged', async ({ browser }) => {
  const ctxA = await browser.newContext();
  const pageA = await ctxA.newPage();
  let sharedKey = '';

  try {
    // ── 1. Full buyer login (no 2FA yet) and enroll an authenticator ────────────────────────
    await submitCredentials(pageA, 'buyer');
    await pageA.waitForURL(u => !/\/login\/?$/i.test(u.pathname), { timeout: 20000 });

    await pageA.goto('/Identity/Account/Manage/EnableAuthenticator', { waitUntil: 'domcontentloaded' });
    sharedKey = (await pageA.locator('kbd').first().innerText()).trim();
    expect(sharedKey.length).toBeGreaterThan(10);

    await pageA.fill('#Input_Code', await freshTotp(sharedKey));
    await Promise.all([
      pageA.waitForLoadState('domcontentloaded'),
      pageA.locator('form button[type="submit"], form input[type="submit"]').first().click(),
    ]);
    // Enrollment lands on recovery codes (or back on TwoFactorAuthentication) — never back on the form with errors.
    await expect(pageA.locator('.validation-summary-errors, .text-danger >> text=/invalid/i')).toHaveCount(0);

    // ── 2. Log out, log back in: expect the 2FA challenge; tick "Don't ask again" ──────────
    await logout(pageA);
    await submitCredentials(pageA, 'buyer');
    await pageA.waitForURL(/LoginWith2fa/i, { timeout: 20000 });

    await pageA.fill('#Input_TwoFactorCode', await freshTotp(sharedKey));
    await pageA.check('#Input_RememberMachine'); // ← the checkbox under test
    await Promise.all([
      pageA.waitForURL(u => !/LoginWith2fa|\/login\/?$/i.test(u.pathname + u.search), { timeout: 20000 }),
      pageA.locator('form button[type="submit"]').first().click(),
    ]);

    // ── 3. Log out and log in again on the SAME context: 2FA must be skipped ───────────────
    await logout(pageA);
    await submitCredentials(pageA, 'buyer');
    await pageA.waitForURL(u => !/\/login\/?$/i.test(u.pathname), { timeout: 20000 });
    expect(pageA.url()).not.toMatch(/LoginWith2fa/i); // remembered — no second factor

    // ── 4. A FRESH context (new device) must still be challenged ───────────────────────────
    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await submitCredentials(pageB, 'buyer');
    await pageB.waitForURL(/LoginWith2fa/i, { timeout: 20000 }); // device-bound, not account-wide
    await ctxB.close();
  } finally {
    // ── 5. Cleanup: restore the buyer account to no-2FA regardless of test outcome ─────────
    try {
      // pageA is authenticated (remembered device). Disable 2FA, then reset the authenticator key.
      await pageA.goto('/Identity/Account/Manage/Disable2fa', { waitUntil: 'domcontentloaded' });
      const disableBtn = pageA.locator('form button[type="submit"], form input[type="submit"]').first();
      if (await disableBtn.count()) await disableBtn.click({ timeout: 10000 }).catch(() => {});
      await pageA.goto('/Identity/Account/Manage/ResetAuthenticator', { waitUntil: 'domcontentloaded' });
      const resetBtn = pageA.locator('form button[type="submit"], form input[type="submit"]').first();
      if (await resetBtn.count()) await resetBtn.click({ timeout: 10000 }).catch(() => {});
    } catch { /* best-effort cleanup */ }
    await ctxA.close();
  }

  // ── 6. Prove cleanup worked: a fresh context logs in with password only ──────────────────
  const ctxC = await browser.newContext();
  const pageC = await ctxC.newPage();
  await submitCredentials(pageC, 'buyer');
  await pageC.waitForURL(u => !/\/login\/?$/i.test(u.pathname), { timeout: 20000 });
  expect(pageC.url()).not.toMatch(/LoginWith2fa/i);
  await ctxC.close();
});
