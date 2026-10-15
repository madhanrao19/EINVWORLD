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

  const freq = v => page.evaluate(v => window.jQuery('select[name="Invoice.InvoicePeriod"]').val(v).trigger('change'), v);
  const startCol = page.locator('#startDate-display').locator('xpath=ancestor::div[contains(@class,"col-md-4")][1]');
  await freq('Monthly');
  await expect(startCol).toBeVisible();
  await page.evaluate(() => { window.jQuery('#startDate')[0]._flatpickr.clear(); window.jQuery('#endDate')[0]._flatpickr.clear(); window.nextStep(); });
  await expect(page.locator('#billingPeriodError')).toContainText('start and an end date');
  await expect(page.locator('#step1')).toBeVisible();
  await freq('Not_Applicable');
  await expect(startCol).toBeHidden();
  await expect(page.locator('#billingPeriodError')).toBeHidden();
});
