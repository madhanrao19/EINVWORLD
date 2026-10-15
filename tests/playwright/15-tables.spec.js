// Shared data tables (einvworld-table.js): the Columns menu hides/shows and reorders columns and saves
// the choice to the user's ACCOUNT (api/table-preferences), so it survives cleared browser storage;
// client-sorted tables reorder rows by the clicked header, both directions, also after a column move.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function headers(table) {
  return (await table.locator('thead th:visible').allInnerTexts()).map(t => t.trim());
}

async function resetItems(page) {
  await page.locator('.einv-table-tools button', { hasText: 'Columns' }).click();
  await page.locator('[data-einv-cols-reset]').click();
  await expect(page.locator('.einv-cols-status')).toHaveText('Saved to your account');
  await page.keyboard.press('Escape');
}

test('columns menu: hide + reorder are saved to the account and reset (Items)', async ({ page }) => {
  test.setTimeout(90000);
  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  const table = page.locator('table[data-einv-table="items"]');
  await expect(table).toHaveAttribute('data-einv-table-ready', '');
  await resetItems(page);
  const before = await headers(table);

  await page.locator('.einv-table-tools button', { hasText: 'Columns' }).click();
  const items = page.locator('.einv-cols-item');
  const hideable = items.filter({ has: page.locator('input:not([disabled])') });
  const hiddenName = (await hideable.last().locator('span').innerText()).trim();
  await hideable.last().locator('input').uncheck();
  await expect(table.locator('thead th', { hasText: hiddenName }).first()).toBeHidden();

  // Keyboard reorder: move the second column to the top.
  const second = (await items.nth(1).locator('span').innerText()).replace(' (always shown)', '').trim();
  await items.nth(1).locator('.einv-cols-handle').focus();
  await page.keyboard.press('ArrowUp');
  await expect(items.first()).toHaveAttribute('data-col', second);
  await expect(page.locator('.einv-cols-status')).toHaveText('Saved to your account');
  const moved = await headers(table);
  expect(moved.indexOf(second)).toBeLessThan(moved.indexOf(before[0]));
  // Body cells moved with their header (each full-width row's cells follow the header layout).
  const aligned = await table.evaluate(t => {
    const head = Array.from(t.tHead.rows[0].cells).map(c => c.getAttribute('data-einv-col'));
    return Array.from(t.tBodies[0].rows).filter(r => r.cells.length === head.length)
      .every(r => Array.from(r.cells).map(c => c.getAttribute('data-einv-col')).join() === head.join());
  });
  expect(aligned).toBe(true);

  // Account, not the browser: wipe local storage and reload — the layout is still there.
  await page.evaluate(() => localStorage.clear());
  await page.reload({ waitUntil: 'domcontentloaded' });
  await expect(table.locator('thead th', { hasText: hiddenName }).first()).toBeHidden();
  await expect.poll(() => headers(table)).toEqual(moved);

  await resetItems(page);
  expect(await headers(table)).toEqual(before);
});

test('pointer drag reorders, utility columns stay put, sort follows the moved column', async ({ page, baseURL }) => {
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.setContent(`
    <table data-einv-table="fixture" data-einv-sort="client"><thead><tr>
      <th class="col-checkbox"><input type="checkbox"></th><th>Name</th><th class="text-end">Total</th><th>Issued</th><th class="col-action"></th></tr></thead>
    <tbody>
      <tr><td><input type="checkbox"></td><td>beta</td><td>RM 1,200.50</td><td data-sort-value="2026-10-02">2 Oct 2026</td><td>a</td></tr>
      <tr><td><input type="checkbox"></td><td>Alpha</td><td>RM 99.00</td><td data-sort-value="2026-10-14">14 Oct 2026</td><td>a</td></tr>
      <tr><td><input type="checkbox"></td><td>item 10</td><td>–</td><td data-sort-value="2025-12-31">31 Dec 2025</td><td>a</td></tr>
      <tr><td colspan="5">footer row stays put</td></tr>
    </tbody></table>`);
  await page.addScriptTag({ url: baseURL + '/tabler/js/einvworld-table.js' });
  await page.locator('.einv-table-tools button', { hasText: 'Columns' }).click();
  await page.evaluate(() => { document.querySelector('.einv-cols-menu').classList.add('show'); }); // no Bootstrap JS on this fixture

  // Drag "Issued" above "Name".
  const handle = page.locator('.einv-cols-item[data-col="Issued"] .einv-cols-handle');
  const target = page.locator('.einv-cols-item[data-col="Name"]');
  const hb = await handle.boundingBox(), tb = await target.boundingBox();
  await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2);
  await page.mouse.down();
  await page.mouse.move(tb.x + 10, tb.y + 2, { steps: 5 });
  await page.mouse.up();

  const head = (await page.locator('thead th').allInnerTexts()).map(t => t.trim());
  expect(head).toEqual(['', 'Issued', 'Name', 'Total', '']); // checkbox first, action last, as authored
  expect((await page.locator('tbody tr').first().locator('td').allInnerTexts()).map(t => t.trim()))
    .toEqual(['', '2 Oct 2026', 'beta', 'RM 1,200.50', 'a']);
  await expect(page.locator('td[colspan]')).toHaveText('footer row stays put');

  // Sorting "Total" (now column 4) sorts by Total, not by whatever sat there at load.
  await page.locator('thead th', { hasText: 'Total' }).locator('button.einv-sort').click();
  expect((await page.locator('tbody tr td:nth-child(4)').allInnerTexts()).slice(0, 3).map(t => t.trim()))
    .toEqual(['RM 99.00', 'RM 1,200.50', '–']);

  // Rows a page script adds later get the same layout.
  await page.evaluate(() => {
    const tr = document.createElement('tr');
    tr.innerHTML = '<td><input type="checkbox"></td><td>new</td><td>RM 1.00</td><td>1 Jan 2026</td><td>a</td>';
    document.querySelector('tbody').prepend(tr);
  });
  await expect.poll(async () => (await page.locator('tbody tr').first().locator('td').allInnerTexts()).map(t => t.trim()))
    .toEqual(['', '1 Jan 2026', 'new', 'RM 1.00', 'a']);
});

test('client sort: text, RM amounts, dates and placeholders, both directions', async ({ page, baseURL }) => {
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.setContent(`
    <table data-einv-table="fixture2" data-einv-sort="client"><thead><tr>
      <th>Name</th><th class="text-end">Total</th><th>Issued</th><th class="col-action"></th></tr></thead>
    <tbody>
      <tr><td>beta</td><td>RM 1,200.50</td><td data-sort-value="2026-10-02">2 Oct 2026</td><td></td></tr>
      <tr><td>Alpha</td><td>RM 99.00</td><td data-sort-value="2026-10-14">14 Oct 2026</td><td></td></tr>
      <tr><td>item 10</td><td>–</td><td data-sort-value="2025-12-31">31 Dec 2025</td><td></td></tr>
      <tr><td>item 9</td><td>RM 10,000.00</td><td data-sort-value="2026-01-05">5 Jan 2026</td><td></td></tr>
      <tr><td colspan="4">footer row stays put</td></tr>
    </tbody></table>`);
  await page.addScriptTag({ url: baseURL + '/tabler/js/einvworld-table.js' });
  const col = async i => (await page.locator(`tbody tr td:nth-child(${i})`).allInnerTexts()).slice(0, 4).map(t => t.trim());
  const sortBy = i => page.locator(`thead th:nth-child(${i}) button.einv-sort`).click();

  await sortBy(1);
  expect(await col(1)).toEqual(['Alpha', 'beta', 'item 9', 'item 10']); // case-insensitive, numeric-aware
  await sortBy(2);
  expect(await col(2)).toEqual(['RM 99.00', 'RM 1,200.50', 'RM 10,000.00', '–']); // amounts, placeholder last
  await sortBy(2);
  await expect(page.locator('thead th:nth-child(2)')).toHaveAttribute('aria-sort', 'descending');
  expect(await col(2)).toEqual(['–', 'RM 10,000.00', 'RM 1,200.50', 'RM 99.00']);
  await sortBy(3);
  expect(await col(3)).toEqual(['31 Dec 2025', '5 Jan 2026', '2 Oct 2026', '14 Oct 2026']); // by data-sort-value
  await expect(page.locator('thead th:nth-child(4) button')).toHaveCount(0); // action column not sortable
  await expect(page.locator('td[colspan]')).toHaveText('footer row stays put');
});

test('preferences API: own account only, antiforgery and input checks', async ({ page, request }) => {
  // Signed out: no access.
  const anon = await request.get('/api/table-preferences?keys=items', { maxRedirects: 0 });
  expect([401, 302]).toContain(anon.status());

  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  const call = (method, url, body, withToken = true) => page.evaluate(async ([method, url, body, withToken]) => {
    const headers = { 'Content-Type': 'application/json' };
    if (withToken) headers.RequestVerificationToken = document.querySelector('meta[name="einv-csrf"]').content;
    const r = await fetch(url, { method, headers, body: body ? JSON.stringify(body) : undefined, credentials: 'same-origin' });
    return r.status;
  }, [method, url, body, withToken]);

  expect(await call('PUT', '/api/table-preferences/items', { hidden: [], order: [] }, false)).toBe(400); // no antiforgery token
  expect(await call('PUT', '/api/table-preferences/..%2Fusers', { hidden: [], order: [] })).toBe(400);
  expect(await call('PUT', '/api/table-preferences/items', { hidden: Array.from({ length: 61 }, (_, i) => 'c' + i), order: [] })).toBe(400);
  expect(await call('PUT', '/api/table-preferences/items', { hidden: [], order: [] })).toBe(204);
  expect(await call('GET', '/api/table-preferences?keys=items')).toBe(200);
});

test.describe('phone', () => {
  test.use({ viewport: { width: 390, height: 800 }, hasTouch: true, isMobile: true });
  test('columns menu fits the screen with touch-size handles', async ({ page }) => {
    await login(page, 'supplier');
    await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
    await page.locator('.einv-table-tools button', { hasText: 'Columns' }).click();
    const menu = page.locator('.einv-cols-menu.show');
    await expect(menu).toBeVisible();
    const box = await menu.boundingBox();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(390);
    const handle = await page.locator('.einv-cols-handle').first().boundingBox();
    expect(Math.round(handle.width)).toBeGreaterThanOrEqual(44);
  });
});
