// Invoice Lists (Ledger 3d-1): the shared Columns menu replaces the old Customize panel, with the
// same per-direction column sets, the lean default columns, and a one-time adoption of the choice the
// old panel saved. The migration is tested with intercepted responses, so no old-format data is written.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

test('shared Columns menu with lean defaults and per-direction keys', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  // Start from defaults for this table, whatever an earlier run saved.
  await page.route('**/api/table-preferences?*', r => r.fulfill({ json: {} }));
  await page.route('**/InvoiceLists?handler=LoadColumn*', r => r.fulfill({ json: { success: true, data: null } }));
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });

  const table = page.locator('#invoiceTable');
  await expect(table).toHaveAttribute('data-einv-table', 'invoices');
  await expect(page.locator('#columnSettingsModal')).toHaveCount(0);
  for (const id of ['col-uuid', 'col-submission-id', 'col-rejected-date', 'col-last-updated', 'col-supplier'])
    await expect(table.locator(`th[data-col-id="${id}"]`)).toBeHidden();
  for (const id of ['col-invoice-no', 'col-buyer', 'col-total-amount', 'col-lhdn-status'])
    await expect(table.locator(`th[data-col-id="${id}"]`)).toBeVisible();

  await page.locator('[data-einv-columns-for="invoices"] button', { hasText: 'Columns' }).click();
  await expect(page.locator('.einv-cols-item[data-col="col-invoice-no"] input')).toBeDisabled(); // always shown
  await expect(page.locator('.einv-cols-item[data-col="col-uuid"] input')).not.toBeChecked();

  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Received', { waitUntil: 'domcontentloaded' });
  await expect(table).toHaveAttribute('data-einv-table', 'invoices-received');
  await expect(table.locator('th[data-col-id="col-supplier"]')).toBeVisible();
  await expect(table.locator('th[data-col-id="col-buyer"]')).toBeHidden();
});

test('adopts the old Customize panel choice once, and skips untouched defaults', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  const saved = [];
  await page.route('**/api/table-preferences/**', async r => {
    if (r.request().method() === 'PUT') { saved.push(JSON.parse(r.request().postData())); return r.fulfill({ status: 204 }); }
    return r.continue();
  });
  await page.route('**/api/table-preferences?*', r => r.fulfill({ json: {} })); // nothing on the account yet
  await page.route('**/InvoiceLists?handler=LoadColumnPreferences*', r => r.fulfill({ json: { success: true, data: {
    'col-checkbox': true, 'col-invoice-no': true, 'col-uuid': true, 'col-submission-id': false, 'col-supplier': false,
    'col-buyer': true, 'col-submitted-date': true, 'col-document-type': false, 'col-total-amount': true,
    'col-lhdn-status': true, 'col-internal-status': true, 'col-rejected-date': false, 'col-last-updated': false,
    'col-created-by': true, 'col-action': true } } }));
  await page.route('**/InvoiceLists?handler=LoadColumnOrder*', r => r.fulfill({ json: { success: true, data: ['col-total-amount', 'col-invoice-no'] } }));
  await page.evaluate(() => localStorage.clear()).catch(() => {});
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });

  const table = page.locator('#invoiceTable');
  await expect(table.locator('th[data-col-id="col-uuid"]')).toBeVisible();          // turned on in the old panel
  await expect(table.locator('th[data-col-id="col-document-type"]')).toBeHidden();  // turned off in the old panel
  await expect.poll(() => saved.length).toBe(1);
  expect(saved[0].hidden).toContain('col-document-type');
  expect(saved[0].hidden).not.toContain('col-uuid');
  expect(saved[0].order.slice(0, 2)).toEqual(['col-total-amount', 'col-invoice-no']);
  const ids = await table.locator('thead th[data-col-id]:visible').evaluateAll(ths => ths.map(th => th.dataset.colId));
  expect(ids.indexOf('col-total-amount')).toBeLessThan(ids.indexOf('col-invoice-no'));

  // Untouched defaults from the old handler are not copied to the account.
  saved.length = 0;
  await page.unroute('**/InvoiceLists?handler=LoadColumnPreferences*');
  await page.unroute('**/InvoiceLists?handler=LoadColumnOrder*');
  await page.route('**/InvoiceLists?handler=LoadColumnPreferences*', r => r.fulfill({ json: { success: true, data: {
    'col-uuid': false, 'col-submission-id': false, 'col-supplier': false, 'col-buyer': true, 'col-rejected-date': false,
    'col-last-updated': false, 'col-created-by': true } } }));
  await page.route('**/InvoiceLists?handler=LoadColumnOrder*', r => r.fulfill({ json: { success: true, data: null } }));
  await page.evaluate(() => localStorage.clear());
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
  expect(saved).toEqual([]);
});

test('a change on the Draft tab keeps choices for columns only other tabs have', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  const saved = [];
  await page.route('**/api/table-preferences/**', async r => {
    if (r.request().method() === 'PUT') { saved.push(JSON.parse(r.request().postData())); return r.fulfill({ status: 204 }); }
    return r.continue();
  });
  // The shared "invoices" set says: show UUID (Sent/All only), hide Submission ID, UUID ordered first.
  await page.route('**/api/table-preferences?*', r => r.fulfill({ json: { invoices: { hidden: ['col-submission-id', 'col-supplier'], order: ['col-uuid', 'col-invoice-no'] } } }));
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Draft', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#invoiceTable th[data-col-id="col-uuid"]')).toHaveCount(0); // Draft has no UUID column
  await page.locator('[data-einv-columns-for="invoices"] button', { hasText: 'Columns' }).click();
  await page.locator('.einv-cols-item[data-col="col-document-type"] input').uncheck();
  await expect.poll(() => saved.length).toBe(1);
  expect(saved[0].hidden).toEqual(expect.arrayContaining(['col-submission-id', 'col-supplier', 'col-document-type']));
  expect(saved[0].order).toContain('col-uuid');
});

// v1.47.37 list feedback: short Ref No. header, Date-desc default, closed filter card, Save View reads
// the filter form, Actions pinned on narrow desktops. Save View is intercepted — nothing is written.
test('default Date sort, short Ref No. header, filter card starts closed', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });

  await expect(page.locator('#invoiceTable th[data-col-id="col-ref-doc-no"] a')).toHaveText(/^\s*Ref No\.\s*$/);
  await expect(page.locator('#invoiceTable th[data-col-id="col-submitted-date"] a')).toHaveClass(/text-primary/);
  const stamps = await page.locator('#invoiceTable tbody td.col-submitted-date').evaluateAll(tds => tds.map(td => {
    const m = td.textContent.trim().match(/^(\d{4})-(\d\d)-(\d\d) (\d\d):(\d\d) (AM|PM)$/);
    return m ? Date.UTC(+m[1], m[2] - 1, +m[3], (+m[4] % 12) + (m[6] === 'PM' ? 12 : 0), +m[5]) : null;
  }).filter(v => v !== null));
  for (let i = 1; i < stamps.length; i++) expect(stamps[i]).toBeLessThanOrEqual(stamps[i - 1]);

  const panel = page.locator('#filterOffcanvas');
  await expect(panel).toBeHidden();
  await expect(page.locator('#activeFilterCount')).toHaveCount(0);
  await page.locator('button[data-bs-target="#filterOffcanvas"]').click();
  await expect(panel).toBeVisible();

  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent&searchQuery=EINV&LHDNStatus=Valid', { waitUntil: 'domcontentloaded' });
  await expect(panel).toBeHidden();
  await expect(page.locator('#activeFilterCount')).toHaveText('2');
});

test('Save View stores the filter form values, including unapplied ones', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  const posted = [];
  await page.route('**/InvoiceLists?handler=SaveView', r => { posted.push(JSON.parse(r.request().postData())); return r.fulfill({ json: { success: false, message: 'intercepted' } }); });
  await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });

  // Nothing set: refuses instead of saving an empty view.
  await page.locator('#saveCurrentViewBtn').click();
  await expect(page.locator('.swal2-popup')).toContainText('Set a filter or sort first');
  await page.locator('.swal2-confirm').click();
  expect(posted).toEqual([]);

  await page.locator('button[data-bs-target="#filterOffcanvas"]').click();
  await page.fill('#searchQuery', 'EINV100');           // typed, not applied
  await page.selectOption('#LHDNStatusFilter', 'Valid');
  await page.locator('#saveCurrentViewBtn').click();
  await page.locator('.swal2-input').fill('PW view');
  await page.locator('.swal2-confirm').click();
  await expect.poll(() => posted.length).toBe(1);
  const q = new URLSearchParams(posted[0].queryString);
  expect(q.get('searchQuery')).toBe('EINV100');
  expect(q.get('LHDNStatus')).toBe('Valid');
  expect(q.has('invoiceDirection')).toBe(false);
  expect(posted[0].name).toBe('PW view');
});

for (const vp of [{ width: 1280, height: 1024 }, { width: 1366, height: 768 }]) {
  test(`Actions column stays visible with every column shown at ${vp.width}x${vp.height}`, async ({ page }) => {
    test.setTimeout(90000);
    await loginOrSkip(page, 'supplier');
    await page.setViewportSize(vp);
    await page.route('**/api/table-preferences?*', r => r.fulfill({ json: { invoices: { hidden: [], order: [] } } }));
    await page.route('**/InvoiceLists?handler=LoadColumn*', r => r.fulfill({ json: { success: true, data: null } }));
    await page.goto('/Invoices/InvoiceLists?invoiceDirection=Sent', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('#invoiceTable th[data-col-id="col-uuid"]')).toBeVisible();
    const box = await page.evaluate(() => {
      const inner = document.querySelector('.table-scroll .inner').getBoundingClientRect();
      const cell = document.querySelector('#invoiceTable tbody td.col-action') || document.querySelector('#invoiceTable th.col-action');
      return { overflow: document.querySelector('.table-scroll .inner').scrollWidth - inner.width, right: cell.getBoundingClientRect().right, viewRight: inner.right };
    });
    expect(box.overflow).toBeGreaterThan(0);                   // the table really does scroll here
    expect(box.right).toBeLessThanOrEqual(box.viewRight + 1);  // ...but Actions is still on screen
  });
}
