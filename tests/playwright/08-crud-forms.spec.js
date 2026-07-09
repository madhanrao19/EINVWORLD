const { test, expect } = require('@playwright/test');
const { login, watchPage } = require('./helpers/auth');

// Create-form integrity for the complex, FK-dependent entities. Full submit-create is
// covered end-to-end by the Items lifecycle (06-crud-items); these entities either have
// no UI delete (PublicCustomer) or require pre-existing related records (Templates), so
// here we assert the create page loads for an authorised user, renders its key field,
// and accepts input — without writing un-cleanable staging data.
const FORMS = [
  { name: 'PublicCustomer', url: '/PublicCustomer/Create', field: '#PublicCustomer_CompanyName' },
  { name: 'Templates', url: '/Templates/Create', field: '#InvoiceTemplate_TemplateName' },
  // Suppliers create is reachable by a Supplier only via the lead-conversion flow (by design;
  // direct access is AccessDenied). Use that legitimate entry path.
  { name: 'Suppliers', url: '/Suppliers/Create?from=lead', field: '#PartyInfo_CompanyName' },
];

for (const f of FORMS) {
  test(`crud-form: ${f.name} create page loads and accepts input (supplier)`, async ({ page }) => {
    const { consoleErrors, failedRequests } = watchPage(page);
    await login(page, 'supplier');
    const resp = await page.goto(f.url, { waitUntil: 'domcontentloaded' });
    expect(resp.status(), `${f.name} create page status`).toBeLessThan(400);
    // Not bounced to login/access-denied
    expect(page.url().toLowerCase()).not.toMatch(/login|accessdenied/);
    // Key field renders and accepts input
    const field = page.locator(f.field);
    await expect(field, `${f.name} key field present`).toHaveCount(1);
    await field.fill(`QA-CHECK-${Date.now()}`);
    await expect(field).not.toHaveValue('');
    // A submit control exists (the form is wired)
    const submit = page.locator('form button[type="submit"], form input[type="submit"]');
    expect(await submit.count(), `${f.name} has a submit control`).toBeGreaterThan(0);
    // No server errors / same-origin failures rendering the form
    const server5xx = failedRequests.filter(x => /^5\d\d /.test(x));
    expect(server5xx, `${f.name} 5xx`).toEqual([]);
    expect(consoleErrors, `${f.name} console errors`).toEqual([]);
  });
}
