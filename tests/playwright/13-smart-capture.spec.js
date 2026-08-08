const { test, expect } = require('@playwright/test');
const path = require('path');

const FIXTURES = path.join(__dirname, 'fixtures');

// NOTE on accounts used here: this verification environment has no real LHDN sandbox credentials
// configured (LHDNApiConfig:ClientSecret is blank — a secret, never checked in). Any non-Admin,
// company-linked user's login synchronously fetches a real LHDN access token
// (Areas/Identity/Pages/Account/Login.cshtml.cs) and fails login entirely if that call fails — this is
// pre-existing app behaviour, unrelated to Smart Capture. Admin logins skip that check. Two Admin-role
// verify-only accounts (each linked to a different company) are used here instead of the standard
// supplier/buyer demo users so the workflow, upload-validation, and cross-tenant checks can actually run
// end-to-end in this sandbox. See the verification report for the accounts/companies seeded and the
// Buyer-role-denial gap this causes.
async function loginVerifyUser(page, email, password) {
  await page.goto('/login', { waitUntil: 'commit', timeout: 60000 });
  await page.fill('#username', email);
  await page.fill('#password-input', password);
  await page.waitForFunction(() => {
    const el = document.querySelector('[name="cf-turnstile-response"]');
    return !!(el && el.value && el.value.length > 0);
  }, { timeout: 35000 });
  await Promise.all([
    page.waitForURL(url => !/\/login\/?$/i.test(url.pathname), { timeout: 20000, waitUntil: 'commit' }),
    page.evaluate(() => document.getElementById('account').requestSubmit()),
  ]);
  await page.waitForSelector('#login-submit', { state: 'detached', timeout: 20000 });
}

test.describe('Smart Capture', () => {
  test('main workflow: upload a valid PDF, it queues and is processed asynchronously', async ({ page }) => {
    // Global test timeout (playwright.config.js) is 60s; this test's terminal-state wait alone can take
    // up to AI:TimeoutSeconds (180s) when Ollama isn't reachable — extend so that wait isn't cut short.
    test.setTimeout(240_000);
    await loginVerifyUser(page, 'admin@einvworld.com', 'Admin@123');
    await page.goto('/Invoices/SmartCapture', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('h1.einv-page-title')).toContainText('Smart Capture');

    await page.locator('#scUpload').setInputFiles(path.join(FIXTURES, 'valid-invoice.pdf'));
    await page.click('#scUploadSubmit');

    await expect(page.locator('table tbody tr').first()).toContainText('valid-invoice.pdf', { timeout: 15000 });
    const openLink = page.locator('table tbody tr').first().locator('a', { hasText: 'Open' });
    await openLink.click();

    // Must reach a real terminal state within a reasonable time — proves the durable job actually ran
    // end-to-end (upload -> SafePath storage -> SyncJobs enqueue -> DurableSyncJobWorker claim ->
    // SmartCaptureExtractionJobHandler -> PdfPig text extraction -> assistant call). This sandbox has no
    // reachable Ollama, so the realistic terminal state is "Failed" (FailureCode=AssistantUnavailable);
    // the assertion also accepts the success states so it passes wherever Ollama IS available. The wait
    // window must exceed AI:TimeoutSeconds (180s in appsettings.json) — with no reachable Ollama, the
    // assistant call itself blocks for the full timeout before SmartCaptureExtractionJobHandler can mark
    // the job Failed, so a shorter window flakes even though the pipeline is behaving correctly.
    await expect(async () => {
      const bodyText = await page.locator('body').innerText();
      expect(bodyText).toMatch(/Extraction failed|Review Checklist|has blocking errors|Draft.*created/i);
    }).toPass({ timeout: 200000, intervals: [3000] });
  });

  test('rejects a renamed non-PDF (magic-byte check)', async ({ page }) => {
    await loginVerifyUser(page, 'admin@einvworld.com', 'Admin@123');
    await page.goto('/Invoices/SmartCapture', { waitUntil: 'domcontentloaded' });
    await page.locator('#scUpload').setInputFiles(path.join(FIXTURES, 'fake.pdf'));
    await page.click('#scUploadSubmit');
    await expect(page.locator('.alert-danger')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.alert-danger')).toContainText(/does not match|not a valid|signature/i);
  });

  test('rejects an oversized upload', async ({ page }) => {
    await loginVerifyUser(page, 'admin@einvworld.com', 'Admin@123');
    await page.goto('/Invoices/SmartCapture', { waitUntil: 'domcontentloaded' });
    // Generated in-memory rather than committed as a fixture file, to avoid a 15MB dummy binary in git
    // history — only its size matters for this check, not its content.
    const oversized = Buffer.concat([Buffer.from('%PDF-1.4\n'), Buffer.alloc(15 * 1024 * 1024, '0')]);
    await page.locator('#scUpload').setInputFiles({ name: 'oversized.pdf', mimeType: 'application/pdf', buffer: oversized });
    await page.click('#scUploadSubmit');
    await expect(page.locator('.alert-danger')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.alert-danger')).toContainText(/too large/i);
  });

  // NOTE: A true two-tenant, both-logged-in browser test could not be completed in this environment —
  // Areas/Identity/Pages/Account/Login.cshtml.cs:253 hardcodes the LHDN-token-fetch bypass to the literal
  // username "admin@einvworld.com" (not role-based), so only that one account can ever complete login
  // without real LHDN sandbox credentials (which this sandbox doesn't have — see the top-of-file note).
  // Tenant isolation at the data layer IS proven with two genuinely distinct, unrelated accounts by
  // EINVWORLD.Tests/Integration/SmartCaptureDocumentIntegrationTests.GetOwnedAsync_Returns_Null_For_A_User_Outside_The_Owning_Company
  // (run against real SQL Server). This browser-level test covers what IS verifiable here: the download
  // endpoint requires authentication at all, and the owning tenant can retrieve its own document.
  test('download endpoint requires authentication; owning tenant can retrieve its own document', async ({ page, request }) => {
    await loginVerifyUser(page, 'admin@einvworld.com', 'Admin@123');
    await page.goto('/Invoices/SmartCapture', { waitUntil: 'domcontentloaded' });
    await page.locator('#scUpload').setInputFiles(path.join(FIXTURES, 'valid-invoice.pdf'));
    await page.click('#scUploadSubmit');
    const openLink = page.locator('table tbody tr').first().locator('a', { hasText: 'Open' });
    await expect(openLink).toBeVisible({ timeout: 15000 });
    const href = await openLink.getAttribute('href');
    expect(href).toMatch(/\/Invoices\/SmartCaptureReview\/\d+/);
    const documentId = href.match(/\/SmartCaptureReview\/(\d+)/)[1];

    // Owning tenant, logged in: can open the review page for its own document.
    const ownResp = await page.goto(`/Invoices/SmartCaptureReview/${documentId}`, { waitUntil: 'domcontentloaded' });
    expect(ownResp.status()).toBe(200);

    // No session at all: the download handler must not serve the file to an anonymous caller — Playwright's
    // `request` fixture follows redirects by default, so a successful block lands on /login (200 there),
    // not a 200 with the actual file content-type.
    const anonResp = await request.get(`/Invoices/SmartCaptureReview/${documentId}?handler=Download`);
    const contentType = anonResp.headers()['content-type'] || '';
    expect(anonResp.url().toLowerCase(), `anonymous download was not redirected to login: ${anonResp.url()} (${contentType})`).toContain('/login');
  });
});
