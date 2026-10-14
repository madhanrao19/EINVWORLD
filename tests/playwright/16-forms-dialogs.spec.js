// Forms + dialogs (Ledger component layer): inputs have a visible 3:1 outline, SweetAlert2 buttons
// take the brand colours from CSS (no Swal.fire patch) while an explicit confirmButtonColor still
// wins, and toastr toasts are opaque with the fixed deep tones.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const css = (page, selector, prop) =>
  page.locator(selector).first().evaluate((el, p) => getComputedStyle(el).getPropertyValue(p), prop);

test('form controls use the input-border token', async ({ page }) => {
  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  const token = await page.evaluate(() => {
    const probe = document.createElement('div');
    probe.style.color = getComputedStyle(document.documentElement).getPropertyValue('--einv-input-border');
    document.body.appendChild(probe);
    const c = getComputedStyle(probe).color;
    probe.remove();
    return c;
  });
  // Page fields, not the borderless topbar search pill.
  expect(await css(page, '#einv-main-content input.form-control', 'border-top-color')).toBe(token);
});

test('SweetAlert2 buttons are branded by CSS; explicit colours still win', async ({ page }) => {
  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  const fill = await page.evaluate(() => {
    const probe = document.createElement('div');
    probe.style.color = getComputedStyle(document.documentElement).getPropertyValue('--einv-fill');
    document.body.appendChild(probe);
    const c = getComputedStyle(probe).color;
    probe.remove();
    return c;
  });

  await page.evaluate(() => { window.Swal.fire({ title: 'Check', showCancelButton: true }); });
  await expect(page.locator('.swal2-confirm')).toBeVisible();
  expect(await css(page, '.swal2-confirm', 'background-color')).toBe(fill);
  expect(await css(page, '.swal2-cancel', 'background-color')).not.toBe('rgb(110, 120, 129)'); // not the library grey
  await page.locator('.swal2-cancel').click();
  await expect(page.locator('.swal2-popup')).toBeHidden();

  await page.evaluate(() => { window.Swal.fire({ title: 'Delete?', confirmButtonColor: '#d33' }); });
  await expect(page.locator('.swal2-confirm')).toBeVisible();
  expect(await css(page, '.swal2-confirm', 'background-color')).toBe('rgb(221, 51, 51)');
});

test('toastr toasts are opaque with the deep tones', async ({ page }) => {
  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => { window.toastr.success('Saved'); window.toastr.error('Failed'); });
  await expect(page.locator('#toast-container .toast-success')).toBeVisible();
  expect(await css(page, '#toast-container .toast-success', 'background-color')).toBe('rgb(0, 105, 72)');
  expect(await css(page, '#toast-container .toast-error', 'background-color')).toBe('rgb(163, 39, 31)');
  // jQuery fades the toast in; poll until it settles on the CSS opacity.
  await expect.poll(() => css(page, '#toast-container .toast-success', 'opacity')).toBe('1');
});
