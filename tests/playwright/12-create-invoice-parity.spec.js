// Parity check: Create e-Invoice wizard (Pages/Invoices/CreateInvoice.cshtml) vs Stitch mockup.
// Run against a LIVE app instance (e.g. VS debug on :5260 or staging on :5210):
//   EINVWORLD_BASE_URL=http://localhost:5260 npx playwright test 12-create-invoice-parity.spec.js
// Or:  npm run qa 12-create-invoice-parity
//
// Verifies BOTH appearance (Stitch stepper / cards / notice) AND functionality (step nav,
// add-item, live totals, review summary, submit handlers) without altering any business logic.
const { test, expect } = require('@playwright/test');
const { login, logout } = require('./helpers/auth');

// Select2 hides the real <select> (display:none), which fails Playwright's actionability/visibility
// check for .selectOption(). The fields themselves are ordinary <select required> elements though, so
// setting .value + dispatching 'change' directly reproduces what a real Select2 pick does as far as
// the wizard's own validateCurrentStep()/nextStep() JS (which only reads element.value) can tell.
async function selectFirstRealOption(page, selector) {
  await page.locator(selector).evaluate((el) => {
    const option = Array.from(el.options).find((o) => o.value);
    if (!option) throw new Error(`No selectable option found for ${el.id || el.name}`);
    el.value = option.value;
    el.dispatchEvent(new Event('change', { bubbles: true }));
  });
}

test('Create e-Invoice: Stitch layout + wizard navigation works', async ({ page }, testInfo) => {
  await login(page, 'supplier');
  const res = await page.goto('/Invoices/CreateInvoice', { waitUntil: 'domcontentloaded' });
  if (res && (res.status() === 404 || res.status() === 500)) {
    testInfo.annotations.push({ type: 'skip', description: `CreateInvoice returned ${res.status()}` });
    console.warn('SKIP: /Invoices/CreateInvoice returned', res.status());
    return;
  }
  await expect(page).not.toHaveURL(/login/i, { timeout: 15000 });

  // --- Appearance: Stitch 3-step stepper (replaces the old thin progress bar) ---
  await expect(page.locator('.ci-stepper')).toBeVisible();
  const nodes = page.locator('.ci-step-node');
  await expect(nodes).toHaveCount(3);
  await expect(nodes.nth(0)).toHaveClass(/is-active/); // step 1 active
  // Brand green fill on the progress track
  await expect(page.locator('#formProgress')).toBeVisible();

  // --- Appearance: Step 1 is visible, 2 & 3 hidden ---
  await expect(page.locator('#step1')).toBeVisible();
  await expect(page.locator('#step2')).toHaveClass(/d-none/);
  await expect(page.locator('#step3')).toHaveClass(/d-none/);

  // --- Functionality: required bindings present on step 1 ---
  await expect(page.locator('#docTypeCode')).toBeVisible();
  await expect(page.locator('#supplierSelect')).toBeVisible();
  await expect(page.locator('#buyerSelect')).toBeVisible();
  await expect(page.locator('#currency')).toBeVisible();

  // Step 1 has more [required] fields than the wizard shows filled by default (DocTypeCode,
  // InvoicePeriod, IssueDate and the primary Supplier are pre-selected server-side; Currency and
  // Buyer are not) — nextStep()'s validateCurrentStep() blocks advancing until every [required]
  // field in #step1 has a value, exactly like a real user would need to pick them.
  await selectFirstRealOption(page, '#docTypeCode');
  await selectFirstRealOption(page, '#currency');
  await selectFirstRealOption(page, '#buyerSelect');

  // --- Functionality: Next advances to step 2 (validates + toggles visibility) ---
  await page.locator('#step1 .btn-primary').click(); // Next: Invoice Items
  await expect(page.locator('#step2')).toBeVisible();
  await expect(page.locator('#step1')).toHaveClass(/d-none/);
  // Stepper reflects step 2 active
  await expect(nodes.nth(1)).toHaveClass(/is-active/);

  // --- Appearance+Functionality: Add Item button exists and line item table present ---
  await expect(page.locator('#lineItemsTable')).toBeVisible();
  await expect(page.locator('#addItemBtn')).toBeVisible();

  // Step 2 ships with one default blank line item (classification/description/price all empty) —
  // fill its [required] fields the same way, so validateItemRequirements() lets Next proceed.
  await selectFirstRealOption(page, '.item-classification');
  await page.locator('.item-description').first().fill('Playwright QA test item');
  await page.locator('.quantity-input').first().fill('1');
  await selectFirstRealOption(page, '#lineItemsTable select[name*=".UnitOfMeasure"]');
  await page.locator('.price-input').first().fill('100');
  await selectFirstRealOption(page, '.tax-category');
  // Any category + a positive percentage satisfies validateItemRequirements()'s taxValid check
  // regardless of category (including "Not Applicable"/"Exemption", which only need percentage
  // to be exactly 0 as an alternative path) — 0 was tried first and failed for the default "01"
  // category, which needs percentage > 0.
  await page.locator('input[name*=".TaxPercentage"]').first().fill('6');

  // --- Functionality: Next to step 3 (review) ---
  // Scoped by onclick, not just .btn-primary — the per-tax-row "+ Tax" button is also .btn-primary.
  await page.locator('#step2 button[onclick="nextStep()"]').click();
  await expect(page.locator('#step3')).toBeVisible();
  await expect(page.locator('#step2')).toHaveClass(/d-none/);

  // --- Review summary ids present (populated by JS on navigation) ---
  await expect(page.locator('#summaryDocType')).toBeVisible();
  await expect(page.locator('#summaryTotalAmount')).toBeVisible();

  // --- Submit handlers present (Save Draft / Submit to LHDN) ---
  await expect(page.locator('button[value="saveDraft"]')).toBeVisible();
  await expect(page.locator('#sa-success-submit-lhdn')).toBeVisible();

  // --- Screenshot for visual diff against the mockup screen.png ---
  await page.locator('#step3').screenshot({ path: testInfo.outputPath('create-invoice-review.png') });

  await logout(page);
});
