const { test, expect, request } = require('@playwright/test');
const { login } = require('./helpers/auth');

const LOCAL_ASSETS = [
  '/assets/libs/jquery/dist/jquery.min.js',
  '/assets/libs/select2/js/select2.min.js',
  '/assets/libs/select2/css/select2.min.css',
  '/assets/libs/select2-bootstrap-5-theme/select2-bootstrap-5-theme.min.css',
  '/assets/libs/toastr/toastr.min.js',
  '/assets/libs/toastr/toastr.min.css',
  '/assets/libs/flatpickr/flatpickr.min.js',
  '/assets/libs/flatpickr/flatpickr.min.css',
  '/assets/libs/sweetalert2/sweetalert2.min.js',
  '/assets/libs/chart.js/chart.umd.js',
  '/assets/libs/font-awesome/css/all.min.css',
  '/assets/libs/font-awesome/webfonts/fa-solid-900.woff2',
  '/assets/libs/jquery-validation/dist/jquery.validate.min.js',
  '/assets/libs/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js',
  '/assets/js/tinymce/skins/ui/oxide/skin.min.css',
];

test('assets: every localized vendor file returns 200', async ({ baseURL }) => {
  const ctx = await request.newContext({ baseURL });
  const bad = [];
  for (const a of LOCAL_ASSETS) {
    const r = await ctx.get(a);
    if (r.status() !== 200) bad.push(`${r.status()} ${a}`);
  }
  await ctx.dispose();
  expect(bad, 'localized assets not returning 200').toEqual([]);
});

test('assets: no external CDN requests on app-controlled public pages', async ({ page }) => {
  // Home/Login/Register load only first-party assets now. (The Contact page is excluded
  // here because its Google Map deliberately pulls Google resources — covered by CSP.)
  const external = new Set();
  page.on('request', req => {
    try {
      const u = new URL(req.url());
      if (u.protocol !== 'http:' && u.protocol !== 'https:') return; // skip data:/blob:/about:
      // Allowed: Turnstile bot widget + optional analytics only. NO asset CDNs.
      const allowed = /localhost|127\.0\.0\.1|challenges\.cloudflare\.com|googletagmanager\.com|google-analytics\.com/.test(u.host);
      if (!allowed) external.add(u.host);
    } catch { /* ignore */ }
  });
  for (const p of ['/', '/login', '/register']) {
    await page.goto(p, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1000);
  }
  expect([...external], 'unexpected external hosts still requested').toEqual([]);
});

test('assets: JS libraries load on the login page', async ({ page }) => {
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1000);
  const globals = await page.evaluate(() => ({
    jquery: typeof window.jQuery,
    swal: typeof window.Swal,
    select2: !!(window.jQuery && window.jQuery.fn && window.jQuery.fn.select2),
    toastr: typeof window.toastr,
    flatpickr: typeof window.flatpickr,
  }));
  expect(globals.jquery).toBe('function');
  expect(globals.select2, 'select2 plugin registered').toBe(true);
  expect(globals.flatpickr).not.toBe('undefined');
});

test('assets: Chart.js loads on the supplier dashboard', async ({ page }) => {
  await login(page, 'supplier');
  await page.waitForTimeout(1500);
  const hasChart = await page.evaluate(() => typeof window.Chart !== 'undefined');
  expect(hasChart, 'Chart global present').toBe(true);
  // Preloader must not be stuck covering the page.
  const preloaderVisible = await page.evaluate(() => {
    const p = document.getElementById('preloader');
    if (!p) return false;
    const s = getComputedStyle(p);
    return s.visibility !== 'hidden' && s.opacity !== '0' && s.display !== 'none';
  });
  expect(preloaderVisible, 'preloader overlay should be hidden').toBe(false);
});
