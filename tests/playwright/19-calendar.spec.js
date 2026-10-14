// One calendar system-wide (v1.35.0): every date field is the shared Flatpickr calendar showing
// "14 Oct 2026", and — the money-safety check — the value each field POSTS is exactly the format it
// posted before the change. Read-only: never saves a draft or submits anything.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

const SHOWN_DAY = /^\d{1,2} (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4}$/;

// Sets a date through the picker's own API (as a click would) and returns { posted, shown }.
async function pick(page, id, date) {
  return page.evaluate(([id, date]) => {
    const input = document.getElementById(id);
    const fp = input && input._flatpickr;
    if (!fp) return null;
    fp.setDate(date, true);
    return { posted: input.value, shown: fp.altInput ? fp.altInput.value : null, type: input.type };
  }, [id, date]);
}

async function nativeDateFieldsShown(page) {
  return page.evaluate(() => Array.from(document.querySelectorAll('input[type="date"], input[type="month"], input.flatpickr-mobile'))
    .filter(el => el.offsetParent !== null).map(el => el.id || el.name));
}

test('dashboard period: Month and Day post the same values as before', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Dashboard/Dashboard?FilterType=Month', { waitUntil: 'domcontentloaded' });
  const shown = page.locator('#filterForm input.einv-date-display');
  await expect(shown).toBeVisible();
  expect(await nativeDateFieldsShown(page)).toEqual([]);
  expect(await page.locator('#filterDate').inputValue()).toMatch(/^\d{4}-\d{2}$/);
  await expect(shown).toHaveValue(/^[A-Z][a-z]{2} \d{4}$/);

  // Month grid: choose March, Apply → FilterDate=YYYY-03 (the old <input type=month> value).
  await shown.click();
  await page.locator('.flatpickr-calendar.open .flatpickr-monthSelect-month', { hasText: 'Mar' }).click();
  await page.locator('#filterForm button[type="submit"]').click();
  await expect(page).toHaveURL(/FilterDate=\d{4}-03(&|$)/);

  // Day mode: same calendar, day grid, posts YYYY-MM-DD.
  await page.selectOption('#filterType', 'Day');
  await expect(shown).toHaveValue(SHOWN_DAY);
  const r = await pick(page, 'filterDate', '2026-10-14');
  expect(r).toEqual({ posted: '2026-10-14', shown: '14 Oct 2026', type: 'hidden' });
  await page.locator('#filterForm button[type="submit"]').click();
  await expect(page).toHaveURL(/FilterType=Day/);
  await expect(page).toHaveURL(/FilterDate=2026-10-14/);

  // Year mode hides the calendar entirely (year dropdown instead).
  await page.selectOption('#filterType', 'Year');
  await expect(page.locator('#filterForm input.einv-date-display')).toHaveCount(0);
  await expect(page.locator('#filterYear')).toBeVisible();
});

test('create invoice: issue/start/end/original dates keep their posted formats', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#issueDate-display')).toBeVisible();

  // Issue date: posts "Y-m-d H:i" (unchanged), shows the date with its time.
  expect(await page.locator('#issueDate').inputValue()).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
  await expect(page.locator('#issueDate-display')).toHaveValue(/^\d{1,2} [A-Z][a-z]{2} \d{4}, \d{1,2}:\d{2} (AM|PM)$/);

  // Billing period: still posts "d-M-Y" as before.
  for (const id of ['startDate', 'endDate']) {
    const r = await pick(page, id, new Date(2026, 9, 14));
    expect(r, id).toEqual({ posted: '14-Oct-2026', shown: '14 Oct 2026', type: 'hidden' });
  }
  // Original invoice date (credit/debit/refund notes): was a native date field, posts "Y-m-d".
  const o = await pick(page, 'originalInvoiceDate', '2026-10-14');
  expect(o).toEqual({ posted: '2026-10-14', shown: '14 Oct 2026', type: 'hidden' });
  expect(await page.locator('label[for="originalInvoiceDate-display"]').count()).toBeGreaterThan(0);
  // Optional fields can be emptied again (the visible field is read-only text).
  const cleared = await page.evaluate(() => {
    const fp = document.getElementById('originalInvoiceDate')._flatpickr;
    fp.calendarContainer.querySelector('.einv-date-clear').click();
    return [document.getElementById('originalInvoiceDate').value, fp.altInput.value];
  });
  expect(cleared).toEqual(['', '']);
  // Required fields get no Clear button.
  expect(await page.evaluate(() => !!document.getElementById('issueDate')._flatpickr.calendarContainer.querySelector('.einv-date-clear'))).toBe(false);
  expect(await nativeDateFieldsShown(page)).toEqual([]);
});

test('invoice lists and recurring invoices: former native fields post Y-m-d', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Invoices/InvoiceLists', { waitUntil: 'domcontentloaded' });
  for (const id of ['submissionReceivedFrom', 'submissionReceivedTo', 'submissionDateFromExp', 'submissionDateToExp']) {
    const r = await pick(page, id, '2026-10-14');
    if (r === null) continue; // field not rendered for this role/tab
    expect(r, id).toEqual({ posted: '2026-10-14', shown: '14 Oct 2026', type: 'hidden' });
  }
  expect(await nativeDateFieldsShown(page)).toEqual([]);
  // Export modal: calendar renders inside the modal so Bootstrap's focus trap doesn't fight it.
  const inModal = await page.evaluate(() => {
    const el = document.getElementById('submissionDateFromExp');
    return el && el._flatpickr ? !!el._flatpickr.calendarContainer.closest('.modal') : null;
  });
  if (inModal !== null) expect(inModal).toBe(true);

  const resp = await page.goto('/RecurringInvoices/Create', { waitUntil: 'domcontentloaded' });
  if (resp && resp.ok() && /RecurringInvoices\/Create/i.test(page.url())) {
    const r = await pick(page, 'Input_NextRunDate', '2026-10-14');
    expect(r).toEqual({ posted: '2026-10-14', shown: '14 Oct 2026', type: 'hidden' });
    expect(await nativeDateFieldsShown(page)).toEqual([]);
  }
});

test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 800 }, hasTouch: true, isMobile: true });
  test('same calendar on a phone, not the native picker', async ({ page }) => {
    test.setTimeout(90000);
    await loginOrSkip(page, 'supplier');
    await page.goto('/Dashboard/Dashboard?FilterType=Day', { waitUntil: 'domcontentloaded' });
    const shown = page.locator('#filterForm input.einv-date-display');
    await expect(shown).toHaveValue(SHOWN_DAY);
    expect(await nativeDateFieldsShown(page)).toEqual([]);
    await shown.click();
    await expect(page.locator('.flatpickr-calendar.open')).toBeVisible();
  });
});
