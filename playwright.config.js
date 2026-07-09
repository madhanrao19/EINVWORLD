// @ts-check
const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests/playwright',
  timeout: 60000,
  // Run serially by default: the app is a single shared instance against a shared
  // staging DB, so parallel CRUD would interfere. Override per-file where safe.
  workers: 1,
  fullyParallel: false,
  reporter: [['line'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.EINVWORLD_BASE_URL || 'http://localhost:5210',
    // 'domcontentloaded' (not 'load') so a blocked third-party CDN/analytics host can't
    // stall navigations for ~20s waiting on the window 'load' event.
    navigationTimeout: 30000,
    actionTimeout: 15000,
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
  },
});
