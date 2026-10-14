// Shared data tables (einvworld-table.js): the Columns menu hides/shows columns and remembers the
// choice; client-sorted tables reorder rows by the clicked header, both directions.
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

test('columns menu hides a column, remembers it, and resets (Items)', async ({ page }) => {
  await login(page, 'supplier');
  await page.goto('/Items/Index', { waitUntil: 'domcontentloaded' });
  const table = page.locator('table[data-einv-table="items"]');
  await expect(table).toHaveAttribute('data-einv-table-ready', '');

  const toggle = page.locator('.einv-table-tools button', { hasText: 'Columns' });
  await toggle.click();
  const item = page.locator('.einv-cols-item').filter({ has: page.locator('input:not([disabled])') }).first();
  const name = (await item.locator('span').innerText()).trim();
  await item.locator('input').uncheck();
  const header = table.locator('thead th', { hasText: name }).first();
  await expect(header).toBeHidden();

  await page.reload({ waitUntil: 'domcontentloaded' });
  await expect(table.locator('thead th', { hasText: name }).first()).toBeHidden();

  await page.locator('.einv-table-tools button', { hasText: 'Columns' }).click();
  await page.locator('[data-einv-cols-reset]').click();
  await expect(table.locator('thead th', { hasText: name }).first()).toBeVisible();
});

test('client sort: text, RM amounts, dates and placeholders, both directions', async ({ page, baseURL }) => {
  // Fixture table (no demo data needed): served from the app origin so the real script loads.
  await page.goto('/login', { waitUntil: 'domcontentloaded' });
  await page.setContent(`
    <table data-einv-table="fixture" data-einv-sort="client"><thead><tr>
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
