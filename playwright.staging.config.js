// @ts-check
// Staging QA config: real Chrome channel + headed, so Cloudflare Turnstile (real site key,
// invisible mode) issues a token the way it does for a normal browser.
const base = require('./playwright.config');

module.exports = {
  ...base,
  use: {
    ...base.use,
    channel: 'chrome',
    headless: false,
  },
};
