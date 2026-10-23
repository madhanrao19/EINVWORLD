// Invoice Edit (Ledger 3e-2): same wizard look as Create Invoice, the billing-period rule, and Save as
// template on every step (invoice mode). Needs one draft to open: the newest draft in the supplier's Draft
// tab is used; the test is skipped when there is none. Read-only: never saves or submits.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function openNewestDraft(page) {
  try { await login(page, 'supplier'); } catch { test.skip(true, 'supplier login failed or is 2FA-gated'); }
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Draft', { waitUntil: 'domcontentloaded' });
  const link = page.locator('#invoiceTable a[href*="InvoiceEdit"]').first();
  if (!(await link.count())) test.skip(true, 'no draft invoice to open');
  await page.goto(await link.getAttribute('href'), { waitUntil: 'domcontentloaded' });
}

test('wizard look, Save as template on steps 1-3, billing period rule', async ({ page }) => {
  test.setTimeout(90000);
  await openNewestDraft(page);
  await expect(page.locator('.einv-ci .ci-stepper')).toBeVisible();
  await expect(page.locator('.einv-ci .ci-step-node').first()).toHaveClass(/is-active/);
  await expect(page.locator('.einv-ci .ci-step-track')).toBeHidden(); // progress shown by the active segment
  await expect(page.locator('#step1 .einv-sum #step1SummaryTotal')).toBeVisible();
  for (const step of ['#step1', '#step2', '#step3'])
    await expect(page.locator(`${step} button[data-bs-target="#saveTemplateModal"]`)).toHaveCount(1);

  // Saved billing dates are written in the calendar's own format, so it can't misread the year.
  for (const id of ['startDate', 'endDate']) {
    const v = await page.locator('#' + id).getAttribute('value');
    if (v) expect(v).toMatch(/^\d{2}-[A-Z][a-z]{2}-\d{4}$/);
  }
  // A draft saved without a period (e.g. a credit note) opens as Not Applicable, like Create Invoice.
  await expect(page.locator('select[name="Invoice.InvoicePeriod"]')).not.toHaveValue('');

  const freq = v => page.evaluate(v => window.jQuery('select[name="Invoice.InvoicePeriod"]').val(v).trigger('change'), v);
  const startCol = page.locator('#startDate-display').locator('xpath=ancestor::div[contains(@class,"col-xxl-4")][1]');
  await freq('Monthly');
  await expect(startCol).toBeVisible();
  await page.evaluate(() => { window.jQuery('#startDate')[0]._flatpickr.clear(); window.jQuery('#endDate')[0]._flatpickr.clear(); window.nextStep(); });
  await expect(page.locator('#billingPeriodError')).toContainText('start and an end date');
  await expect(page.locator('#step1')).toBeVisible();
  await freq('Not_Applicable');
  await expect(startCol).toBeHidden();
  await expect(page.locator('#billingPeriodError')).toBeHidden();
});

// A saved line must open with exactly what was saved: the page's start-up scripts (Select2, the
// classification-code pass) may not swap a saved Classification for 022, and a saved Tax Type 06
// (Not Applicable) line opens at 0.00, read-only, as when it is picked. Read-only, works on any draft.
test('saved Classification / Unit / Tax Type survive page start-up; 06 rate locked at 0', async ({ page }) => {
  test.setTimeout(90000);
  await openNewestDraft(page);
  const selects = 'select[name$=".ClassificationCode"], select[name$=".UnitOfMeasure"], select[name$=".TaxCategory"]';
  // What the server rendered (selected attribute), before any start-up script can change it.
  const rendered = await page.$$eval(selects, els => els.map(s => [s.name, s.querySelector('option[selected]')?.value ?? '']));
  await page.waitForTimeout(2000); // start-up passes run at 100/500/1000 ms
  for (const [name, value] of rendered) {
    if (!value) continue; // nothing saved: a default is allowed
    await expect(page.locator(`select[name="${name}"]`), name).toHaveValue(value);
  }
  for (const row of await page.locator('.tax-row').all()) {
    if ((await row.locator('select.tax-category').inputValue()) !== '06') continue;
    const rate = row.locator('input[name$=".TaxPercentage"]');
    await expect(rate).toHaveJSProperty('readOnly', true);
    expect(parseFloat((await rate.inputValue()) || '0')).toBe(0);
  }
});
