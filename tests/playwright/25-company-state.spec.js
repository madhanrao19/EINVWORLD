// Create/Edit Company Step 2 follows Add Buyer: Malaysia -> LHDN State dropdown, any other country ->
// free-text State / Province, only one State field enabled. Read-only: nothing is saved (the round-trip
// test posts a deliberately incomplete form, which the server rejects before saving).
const { test, expect } = require('@playwright/test');
const { login } = require('./helpers/auth');

async function loginOrSkip(page, role) {
  try { await login(page, role); } catch { test.skip(true, `login for '${role}' failed or is 2FA-gated`); }
}

const setCountry = (page, sel, code) => page.evaluate(([s, c]) => window.jQuery(s).val(c).trigger('change'), [sel, code]);
const foreignCountry = (page, sel) => page.$eval(sel, s => [...s.options].map(o => o.value).find(v => v && v !== 'MYS'));
// Enabled controls posting the State value: must always be exactly one.
const enabledStateFields = (page, name) => page.$$eval(`[name="${name}"]`, els => els.filter(e => !e.disabled).map(e => e.tagName));

const forms = [
  { title: 'Create Company', url: '/Suppliers/Create?from=lead', country: '#PartyInfo_CountryCode', text: '#PartyInfo_StateCode_Text', name: 'PartyInfo.StateCode' },
  { title: 'Edit Company', url: null, country: '#countrySelect', text: '#PartyInfo_StateCode_Text', name: 'PartyInfo.StateCode' },
  { title: 'Add Buyer (reference)', url: '/PublicCustomer/Create', country: '#PublicCustomer_CountryCode', text: '#PublicCustomer_StateCode_Text', name: 'PublicCustomer.StateCode' },
];

for (const f of forms) {
  test(`${f.title}: Malaysia shows the LHDN State list, another country a free-text State / Province`, async ({ page }) => {
    test.setTimeout(90000);
    await loginOrSkip(page, 'supplier');
    let url = f.url;
    if (!url) { // the supplier's own company, via its sidebar Company page
      await page.goto('/Dashboard/Dashboard', { waitUntil: 'domcontentloaded' });
      const details = await page.$eval('#einv-sidebar-menu a[href*="/Suppliers/Details"]', a => a.getAttribute('href'));
      url = details.replace('/Details', '/Edit');
    }
    await page.goto(url, { waitUntil: 'domcontentloaded' });

    await setCountry(page, f.country, 'MYS');
    expect(await enabledStateFields(page, f.name)).toEqual(['SELECT']);
    await expect(page.locator(f.text)).toBeHidden();

    await setCountry(page, f.country, await foreignCountry(page, f.country));
    expect(await enabledStateFields(page, f.name)).toEqual(['INPUT']);
    expect(await page.locator(f.text).evaluate(e => !e.classList.contains('d-none') && e.required)).toBe(true);

    await setCountry(page, f.country, 'MYS');
    expect(await enabledStateFields(page, f.name)).toEqual(['SELECT']);
  });
}

test('Create Company keeps a typed foreign State after a validation error', async ({ page }) => {
  test.setTimeout(90000);
  await loginOrSkip(page, 'supplier');
  await page.goto('/Suppliers/Create?from=lead', { waitUntil: 'domcontentloaded' });
  const foreign = await foreignCountry(page, '#PartyInfo_CountryCode');
  await setCountry(page, '#PartyInfo_CountryCode', foreign);
  await page.locator('#PartyInfo_StateCode_Text').evaluate(e => { e.value = 'California'; }); // on the Step 2 tab, not shown yet
  // Company name, TIN etc. left blank on purpose: the server rejects it and re-renders the form.
  const [post] = await Promise.all([
    page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes('/Suppliers/Create')),
    page.evaluate(() => document.getElementById('PartyInfo_StateCode_Text').form.submit()),
  ]);
  expect(post.status()).toBe(200); // re-rendered with errors, not saved (a save redirects)
  await page.waitForLoadState('domcontentloaded');

  await expect(page.locator('#PartyInfo_CountryCode')).toHaveValue(foreign);
  await expect(page.locator('#PartyInfo_StateCode_Text')).toHaveValue('California');
  expect(await enabledStateFields(page, 'PartyInfo.StateCode')).toEqual(['INPUT']);
});
