// Invoice Edit still opens the editor for the two statuses that can be edited (v1.50.46 sends every other status to
// Invoice Details). Read-only: opens the page, never saves or submits. Which invoices to open comes from
// QA_EDITABLE_INVOICES, e.g. "EINV000123:Draft,EINV000124:TransmissionError" (owned by the QA_LOGIN_ROLE user,
// default supplier);
// skipped when unset.
const { test, expect } = require('@playwright/test');
const { login, watchPage } = require('./helpers/auth');

const cases = (process.env.QA_EDITABLE_INVOICES || '').split(',').filter(Boolean).map(s => s.split(':'));

test('editable invoices open in the editor', async ({ page }) => {
  test.skip(!cases.length, 'set QA_EDITABLE_INVOICES');
  test.setTimeout(120000);
  const role = process.env.QA_LOGIN_ROLE || 'supplier';
  const already = (await page.request.get('/Dashboard/Dashboard', { maxRedirects: 0 })).status() === 200; // saved storageState
  if (!already) { try { await login(page, role); } catch { test.skip(true, `${role} login unavailable`); } }

  for (const [invoiceNo, status] of cases) {
    const { consoleErrors, failedRequests } = watchPage(page);
    const resp = await page.goto(`/Invoices/InvoiceEdit/${encodeURIComponent(invoiceNo)}`, { waitUntil: 'domcontentloaded' });
    expect(resp.status(), `${invoiceNo} (${status})`).toBe(200);
    expect(new URL(page.url()).pathname, `${invoiceNo} (${status}) stays on the editor`).toMatch(/\/Invoices\/InvoiceEdit\//i);
    await expect(page.locator('.einv-ci .ci-stepper')).toBeVisible();
    await expect(page.locator('input[name="Invoice.InvoiceNo"]')).toHaveValue(invoiceNo);
    await expect(page.locator('#lineItems .item-row')).toHaveCount(1); // the saved line is rendered
    await page.waitForTimeout(1500);
    expect(consoleErrors, `${invoiceNo} console errors`).toEqual([]);
    expect(failedRequests, `${invoiceNo} failed requests`).toEqual([]);
  }
});
