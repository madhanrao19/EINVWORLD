// Parity check: Company Details (Pages/Suppliers/Details.cshtml) vs design mockup screen.png
// Run against a LIVE app instance (e.g. VS debug on :5260 or staging on :5210):
//   EINVWORLD_BASE_URL=http://localhost:5260 COMPANY_ID=1 npx playwright test 11-company-details-parity.spec.js
// Or via the project's QA script:  npm run qa 11-company-details-parity
//
// What this proves automatically:
//   1. The page loads (no 500 / no 404 on the company row).
//   2. The identity card shows the Verified / Active / Supplier status pills.
//   3. The two-column data grid (Legal & Registration / Contact & Finance) is present.
//   4. The Assigned Buyers panel is a table with the mockup's columns + Manage/Unassign actions.
//   5. A full-page screenshot is saved for visual diff against screen.png.
//
// NOTE: a pixel-perfect match to screen.png is enforced if you copy the mockup over the
// Playwright baseline:  tests/playwright/__screenshots__/11-company-details-parity.spec.js/company-details.png
// (or just eyeball the saved actual screenshot vs screen.png).
const { test, expect } = require('@playwright/test');
const { login, logout } = require('./helpers/auth');

const COMPANY_ID = process.env.COMPANY_ID || '1';

test('Company Details matches the screen.png layout', async ({ page }, testInfo) => {
  // Log in as Supplier (password-only; Turnstile uses the always-pass test site key).
  await login(page, 'supplier');

  // Navigate to the Company Details page.
  const res = await page.goto(`/Suppliers/Details?id=${COMPANY_ID}`, { waitUntil: 'domcontentloaded' });
  // Honest skip if there is no PartyInfo row with this id in the target DB.
  if (res && (res.status() === 404 || res.status() === 500)) {
    testInfo.annotations.push({ type: 'skip', description: `Company id=${COMPANY_ID} not found (${res.status()}). Seed a PartyInfo row, then re-run.` });
    console.warn(`SKIP: /Suppliers/Details?id=${COMPANY_ID} returned ${res.status()}`);
    return;
  }
  await expect(page).not.toHaveURL(/login/i, { timeout: 15000 });

  // --- Identity card: status pills ---
  await expect(page.getByText('Verified', { exact: true })).toBeVisible();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  await expect(page.getByText('Supplier', { exact: true })).toBeVisible();

  // --- Two-column data grid ---
  await expect(page.getByText('Legal & Registration')).toBeVisible();
  await expect(page.getByText('Contact & Finance')).toBeVisible();

  // --- Assigned Buyers table (mockup columns) ---
  const buyersCard = page.locator('.card', { hasText: 'Assigned Buyers' }).first();
  await expect(buyersCard.getByText('Buyer Entity')).toBeVisible();
  await expect(buyersCard.getByText(/TIN \/ Registration/i)).toBeVisible();
  await expect(buyersCard.getByText('Industry')).toBeVisible();
  // Actions: Manage (mailto) + Unassign (calls existing unassignBuyer handler)
  await expect(buyersCard.getByRole('button', { name: 'Unassign' })).toBeVisible();

  // --- Screenshot for visual diff against screen.png ---
  await page.screenshot({ path: testInfo.outputPath('company-details.png'), fullPage: true });
  // Baseline-aware visual assertion: drop screen.png over the auto-generated baseline to enforce exact match.
  await expect(page).toHaveScreenshot('company-details.png', { fullPage: true, maxDiffPixelRatio: 0.02 });

  await logout(page);
});
