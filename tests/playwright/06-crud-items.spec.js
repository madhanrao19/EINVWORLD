const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

// Full CRUD lifecycle for Items (Admin). Create-only QA data, tagged and self-cleaning:
// the test deletes what it creates via the app's own Delete flow.
test.describe.configure({ mode: 'serial' });

const STAMP = Date.now();
const ITEM_CODE = `QA-ITEM-${STAMP}`;
const DESC = `QA automated item ${STAMP} — safe to delete`;
const DESC_EDITED = `${DESC} (edited)`;

async function setSelect2(page, selector, value) {
  await page.evaluate(({ selector, value }) => {
    const sel = document.querySelector(selector);
    sel.value = value;
    sel.dispatchEvent(new Event('change', { bubbles: true }));
  }, { selector, value });
}

// Submit the form CONTAINING the given field. Scoping to a field avoids grabbing the
// sidebar logout form and avoids flaky waves-effect button click-stability.
async function submitFormWith(page, fieldSelector) {
  await page.evaluate((sel) => {
    const form = document.querySelector(sel).closest('form');
    if (form.requestSubmit) form.requestSubmit(); else form.submit();
  }, fieldSelector);
}

test('items CRUD: create → list → edit → delete', async ({ page }) => {
  test.setTimeout(120000);
  // Supplier (not admin): admin has 2FA enrolled, and Items is authorized for Admin+Supplier.
  await login(page, 'supplier');

  // CREATE
  await page.goto('/Items/Create', { waitUntil: 'domcontentloaded' });
  await page.fill('#ItemDescription_ItemCode', ITEM_CODE);
  await setSelect2(page, '#ItemDescription_ClassificationCode', '001');
  await page.fill('#ItemDescription_Description', DESC);
  await Promise.all([
    page.waitForURL(/\/Items(\/Index)?$/i, { timeout: 20000, waitUntil: 'domcontentloaded' }),
    submitFormWith(page, '#ItemDescription_ItemCode'),
  ]);

  // READ — search for it in the list (GET filter via query string)
  await page.goto('/Items?SearchTerm=' + encodeURIComponent(ITEM_CODE), { waitUntil: 'domcontentloaded' });
  const row = page.locator('tbody tr', { hasText: ITEM_CODE });
  await expect(row, 'created item appears in list').toHaveCount(1);
  await expect(row).toContainText(DESC);

  // UPDATE — navigate to Edit by URL (the layout preloader overlay can intercept clicks
  // until window 'load', which a blocked analytics host delays), change description.
  const editHref = await row.locator('a', { hasText: 'Edit' }).getAttribute('href');
  await page.goto(editHref, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('#ItemDescription_Description');
  await page.fill('#ItemDescription_Description', DESC_EDITED);
  await Promise.all([
    page.waitForURL(/\/Items(\/Index)?$/i, { timeout: 20000, waitUntil: 'domcontentloaded' }),
    submitFormWith(page, '#ItemDescription_ItemCode'),
  ]);
  await page.goto('/Items?SearchTerm=' + encodeURIComponent(ITEM_CODE), { waitUntil: 'domcontentloaded' });
  const editedRow = page.locator('tbody tr', { hasText: ITEM_CODE });
  await expect(editedRow, 'edited description persisted').toContainText('(edited)');

  // DELETE — navigate to Delete confirm by URL, submit
  const deleteHref = await editedRow.locator('a', { hasText: 'Delete' }).getAttribute('href');
  await page.goto(deleteHref, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('#ItemDescription_Id', { state: 'attached' });
  await Promise.all([
    page.waitForURL(/\/Items(\/Index)?$/i, { timeout: 20000, waitUntil: 'domcontentloaded' }),
    submitFormWith(page, '#ItemDescription_Id'),
  ]);
  await page.goto('/Items?SearchTerm=' + encodeURIComponent(ITEM_CODE), { waitUntil: 'domcontentloaded' });
  await expect(page.locator('tbody tr', { hasText: ITEM_CODE }), 'item removed after delete').toHaveCount(0);
});
