const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

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

const PAGES = [
  '/Dashboard/Dashboard',
  '/Invoices/CreateInvoice',
  '/Invoices/InvoiceLists',
  '/Invoices/SmartCapture',
  '/Items/Index',
  '/Buyers/Index',
];

for (const vp of VIEWPORTS) {
  test(`screen sizes: supplier pages fit at ${vp.name} (${vp.width}x${vp.height})`, async ({ page }) => {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await login(page, 'supplier');
    const problems = [];
    for (const path of PAGES) {
      await page.goto(path, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(1500);
      const overflow = await page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth);
      if (process.env.SHOTS) await page.screenshot({ path: `${process.env.SHOTS}/${vp.name}${path.replace(/\//g, '_')}.png`, fullPage: true });
      if (overflow > 1) problems.push(`${path}: ${overflow}px horizontal overflow`);
    }
    expect(problems, problems.join('\n')).toEqual([]);
  });
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
