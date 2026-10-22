// Create Invoice (Ledger 3e-1): the billing-period rule decided 2026-10-14 and the Save as template
// button on every step. Read-only: never saves a draft, a template, or submits to LHDN.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

const setFrequency = (page, value) => page.evaluate(v => {
  window.jQuery('select[name="Invoice.InvoicePeriod"]').val(v).trigger('change');
}, value);
const setDate = (page, id, date, fire = true) => page.evaluate(([id, date, fire]) => document.getElementById(id)._flatpickr.setDate(date, fire), [id, date, fire]);

test('billing period: hidden when not applicable, required and ordered when a frequency is chosen', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#startDate-display')).toBeAttached();
  await expect(page.locator('#billingPeriodError')).toBeAttached();

  const startCol = page.locator('#startDate-display').locator('xpath=ancestor::div[contains(@class,"col-xxl-4")][1]');
  await setFrequency(page, 'Not_Applicable');
  await expect(startCol).toBeHidden();

  await setFrequency(page, 'Monthly');
  await expect(startCol).toBeVisible();

  // Leaving step 1 with no dates: inline error, still on step 1.
  await page.evaluate(() => window.nextStep());
  await expect(page.locator('#billingPeriodError')).toBeVisible();
  await expect(page.locator('#billingPeriodError')).toContainText('start and an end date');
  await expect(page.locator('#step1')).toBeVisible();

  // End before start (the end calendar won't go before the start or today when picking in order, so set
  // a future end first, then a later start, as a loaded draft could): inline error.
  const y = new Date().getFullYear() + 1;
  await setDate(page, 'endDate', new Date(y, 0, 10));
  await setDate(page, 'startDate', new Date(y, 0, 20), false); // no change event: as a draft loads
  await page.evaluate(() => window.nextStep());
  await expect(page.locator('#billingPeriodError')).toContainText("can't be before");

  // Valid period: the period error clears (other step-1 checks may still apply).
  await setDate(page, 'endDate', new Date(y, 0, 31));
  await page.evaluate(() => window.nextStep());
  await expect(page.locator('#billingPeriodError')).toBeHidden();
  expect(await page.locator('#startDate').inputValue()).toBe(`20-Jan-${y}`); // posted format unchanged

  // Back to Not applicable: both dates cleared and hidden.
  await page.evaluate(() => { if (!document.getElementById('step1').classList.contains('d-none')) return; window.prevStep(); });
  await setFrequency(page, 'Not_Applicable');
  await expect(startCol).toBeHidden();
  expect(await page.locator('#startDate').inputValue()).toBe('');
  expect(await page.locator('#endDate').inputValue()).toBe('');
});

test('Save as template is on every step and opens the template dialog', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  for (const step of ['#step1', '#step2', '#step3'])
    await expect(page.locator(`${step} button[data-bs-target="#saveTemplateModal"]`)).toHaveCount(1);
  await page.locator('#step1 button[data-bs-target="#saveTemplateModal"]').click();
  await expect(page.locator('#saveTemplateModal')).toBeVisible();
  await expect(page.locator('#TemplateName')).toHaveValue(/^Template_\d{4}-\d{2}-\d{2}$/);
  // The step 3 button (not the first one on the page) also gets the default name.
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.evaluate(() => { document.getElementById('step1').classList.add('d-none'); document.getElementById('step3').classList.remove('d-none'); });
  await page.locator('#step3 button[data-bs-target="#saveTemplateModal"]').click();
  await expect(page.locator('#saveTemplateModal')).toBeVisible();
  await expect(page.locator('#TemplateName')).toHaveValue(/^Template_\d{4}-\d{2}-\d{2}$/);
});

// Issue #308: the shipping recipient's TIN uses a floating label like the other fields, the Validate
// with LHDN button is the same height as the field, and the button still works (no LHDN call here:
// missing details and general TINs are handled in the browser).
test('shipping recipient TIN matches the other fields and Validate still works', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => document.getElementById('shippingRecipientCollapse').classList.add('show'));
  const box = sel => page.locator(sel).evaluate(e => Math.round(e.getBoundingClientRect().height));
  const tin = page.locator('#shippingRecipientTIN');
  await expect(page.locator('.form-floating > label[for="shippingRecipientTIN"]')).toHaveText("Shipping Recipient's TIN");
  expect(await box('#shippingRecipientTIN')).toBe(await box('#shippingRecipientName'));
  expect(await box('.btn-validate-shipping')).toBe(await box('#shippingRecipientTIN'));

  const btn = page.locator('.btn-validate-shipping');
  await btn.click();
  await expect(btn).toHaveText('Missing Info - Retry');
  await tin.fill('EI00000000010');
  await expect(btn).toHaveText('Validate with LHDN'); // typing resets it
  await page.evaluate(() => {
    const t = document.getElementById('shippingRecipientIdType'); t.value = t.options[t.options.length - 1].value;
    document.getElementById('shippingRecipientIdNumber').value = 'NA';
  });
  await btn.click();
  await expect(btn).toHaveText(/Validated/);
});

// Tax Type 06 ("Not Applicable") must be 0% (LHDN CF394). Read-only like the rest of this file.
const rate = page => page.locator('input[name="Invoice.InvoiceLines[0].Taxes[0].TaxPercentage"]');
const pickTax = (page, code) => page.evaluate(c => {
  document.getElementById('step2')?.classList.remove('d-none');
  // What a user's pick fires: Select2 sets the value, then raises select2:select.
  window.jQuery('select[name="Invoice.InvoiceLines[0].Taxes[0].TaxCategory"]').val(c).trigger('change').trigger({ type: 'select2:select', params: { data: { id: c } } });
}, code);

test('Tax Type 06 sets the rate to 0.00 and locks it; another type unlocks it', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await expect(rate(page)).toBeAttached();

  await pickTax(page, '02');
  await rate(page).evaluate(el => { el.value = '8'; el.dispatchEvent(new Event('input', { bubbles: true })); });
  await pickTax(page, '06');
  await expect(rate(page)).toHaveValue(/^0(\.0+)?$/);
  expect(await rate(page).evaluate(el => el.readOnly)).toBe(true);

  await pickTax(page, '02');
  expect(await rate(page).evaluate(el => el.readOnly)).toBe(false);
});

test('a saved line with Tax Type 06 and 8% opens at 0.00, locked, with a note (nothing saved)', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  // Render line 1 the way a legacy saved 06 + 8% line renders on Edit/template/clone (same Razor markup).
  await page.route('**/Invoices/CreateInvoice', async route => {
    const res = await route.fetch();
    let html = await res.text();
    html = html.replace(/(<select[^>]*name="Invoice\.InvoiceLines\[0\]\.Taxes\[0\]\.TaxCategory"[\s\S]*?<option value="06")/, '$1 selected="selected"')
               .replace(/<input[^>]*name="Invoice\.InvoiceLines\[0\]\.Taxes\[0\]\.TaxPercentage"[^>]*>/, tag => tag.replace(/value="[^"]*"/, 'value="8.00"'));
    await route.fulfill({ response: res, body: html });
  });
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => document.getElementById('step2')?.classList.remove('d-none'));

  await expect(page.locator('select[name="Invoice.InvoiceLines[0].Taxes[0].TaxCategory"]')).toHaveValue('06');
  await expect(rate(page)).toHaveValue(/^0(\.0+)?$/);
  expect(await rate(page).evaluate(el => el.readOnly)).toBe(true);
  await expect(page.locator('.einv-tax06-note')).toContainText('Saved with a 8.00% rate');
});
