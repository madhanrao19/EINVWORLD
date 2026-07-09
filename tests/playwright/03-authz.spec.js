const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

const PROTECTED = [
  '/Dashboard/Dashboard',
  '/Invoices/InvoiceLists',
  '/Invoices/CreateInvoice',
  '/Items',
  '/Suppliers',
  '/PublicCustomer/List',
  '/Templates/TemplateLists',
  '/RecurringInvoices',
  '/Profile',
];

const ADMIN_ONLY = [
  '/Admin/Users/ManageUser',
  '/Admin/Logs',
  '/Admin/SystemHealth',
  '/Admin/InvoiceSync',
  '/Admin/SyncJobs',
  '/Admin/AuditLog',
  '/Admin/Webhooks',
  '/Admin/AiSettings',
  '/Admin/Resources/Manage',
  '/Admin/Notifications',
  '/Admin/Notifications/Create',
  '/Admin/Codes/TaxTypes/ListTaxType',
  '/Admin/Codes/CurrencyCodes/ListCurrency',
  '/Admin/Codes/MSICSubCategoryCodes/ListMSICSubCategory',
  '/Admin/Codes/ClassificationCodes/ListClassification',
  '/Admin/Resources/Types',
  '/Admin/Resources/Create',
  '/Lead/List',
];

test('authz: anonymous is redirected to login for every protected page', async ({ page }) => {
  const leaks = [];
  for (const path of [...PROTECTED, ...ADMIN_ONLY]) {
    const resp = await page.goto(path, { waitUntil: 'domcontentloaded' });
    const landed = page.url().toLowerCase();
    const ok = landed.includes('/login') || landed.includes('accessdenied') || resp.status() === 404 || resp.status() === 403 || resp.status() === 401;
    if (!ok) leaks.push(`${path} -> ${resp.status()} at ${landed}`);
  }
  expect(leaks, 'anonymous access leaks').toEqual([]);
});

for (const role of ['supplier', 'buyer']) {
  test(`authz: ${role} cannot reach Admin-only pages`, async ({ page }) => {
    await login(page, role);
    const leaks = [];
    for (const path of ADMIN_ONLY) {
      const resp = await page.goto(path, { waitUntil: 'domcontentloaded' });
      const landed = page.url().toLowerCase();
      const blocked = landed.includes('accessdenied') || landed.includes('forbidden') || landed.includes('/login')
        || [401, 403, 404].includes(resp.status());
      if (!blocked) leaks.push(`${path} -> ${resp.status()} at ${landed}`);
    }
    expect(leaks, `${role} admin-page leaks`).toEqual([]);
  });
}
