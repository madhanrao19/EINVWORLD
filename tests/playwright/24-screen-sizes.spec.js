const { test, expect } = require('@playwright/test');
const { login, loginWithTotp } = require('./helpers/auth');

// The finance/IT team mostly uses 19" monitors: 1440x900 (widescreen) or 1280x1024 (5:4).
// Plus the common laptop, tablet and phone widths.
const VIEWPORTS = [
  { name: '19in-wide', width: 1440, height: 900 },
  { name: '19in-5x4', width: 1280, height: 1024 },
  { name: 'laptop', width: 1366, height: 768 },
  { name: 'tablet-landscape', width: 1024, height: 768 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 390, height: 844 },
];

// Admin is 2FA-enrolled: its arm runs only when ADMIN_TOTP_SECRET (the authenticator key) is set.
const ROLES = ['supplier', 'buyer', 'admin'];

async function signIn(page, role) {
  if (role !== 'admin') return login(page, role);
  test.skip(!process.env.ADMIN_TOTP_SECRET, 'set ADMIN_TOTP_SECRET to cover the admin pages');
  return loginWithTotp(page, 'admin', process.env.ADMIN_TOTP_SECRET);
}

// Every page in the role's own sidebar (so new menu items are covered automatically),
// plus My Profile and the first invoice's details page.
async function pagesFor(page) {
  await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
  const links = await page.$$eval('#einv-sidebar-menu a[href^="/"]', as => as.map(a => a.getAttribute('href')));
  await page.goto('/Invoices/InvoiceLists', { waitUntil: 'domcontentloaded' });
  const details = await page.$$eval('a[href*="/Invoices/InvoiceDetails"]', as => as.map(a => a.getAttribute('href')));
  return [...new Set([...links, '/Profile/Index', ...details.slice(0, 1)])];
}

for (const role of ROLES) {
  for (const vp of VIEWPORTS) {
    test(`screen sizes: ${role} pages fit at ${vp.name} (${vp.width}x${vp.height})`, async ({ page }) => {
      test.setTimeout(300000);
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await signIn(page, role);
      const problems = [];
      for (const path of await pagesFor(page)) {
        await page.goto(path, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1200);
        const overflow = await page.evaluate(() =>
          document.documentElement.scrollWidth - document.documentElement.clientWidth);
        if (process.env.SHOTS) {
          const name = `${role}_${vp.name}${path.replace(/[\/?=&]/g, '_')}.png`;
          await page.screenshot({ path: `${process.env.SHOTS}/${name}`, fullPage: true });
        }
        if (overflow > 1) problems.push(`${path}: ${overflow}px horizontal overflow`);
      }
      expect(problems, problems.join('\n')).toEqual([]);
    });
  }
}

test('screen sizes: Unit dropdown is the same height as Classification on line items', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await login(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);
  const heights = await page.evaluate(() => {
    document.getElementById('step2')?.classList.remove('d-none'); // measure without walking the wizard
    if (!document.querySelector('.item-unit')) document.getElementById('addItemBtn')?.click();
    const h = sel => document.querySelector(`${sel} + .select2-container .select2-selection`)?.getBoundingClientRect().height;
    return { cls: h('.item-classification'), unit: h('.item-unit') };
  });
  expect(heights.unit).toBeTruthy();
  expect(Math.abs(heights.unit - heights.cls)).toBeLessThanOrEqual(1);
});
