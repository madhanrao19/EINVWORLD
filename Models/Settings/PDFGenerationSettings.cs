namespace eInvWorld.Models.Settings
{
    public class PDFGenerationSettings
    {
        public string BaseUrl { get; set; } = null!;
        public int PlaywrightRenderDelayMs { get; set; } = 1000; // Default to 1000ms

        /// <summary>
        /// PDF rendering engine: "DinkToPdf" (default, current/unmaintained wkhtmltopdf) or
        /// "Puppeteer" (headless Chromium via PuppeteerSharp — maintained, no native DLL).
        /// Switch in appsettings to evaluate Puppeteer output before committing to it.
        /// </summary>
        public string Engine { get; set; } = "DinkToPdf";

        /// <summary>
        /// Optional path to a Chromium/Chrome/Edge executable for the Puppeteer engine. If empty,
        /// PuppeteerSharp downloads a private Chromium on first use (needs internet + write access).
        /// On an offline in-house server, point this at an installed browser, e.g.
        /// "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe".
        /// </summary>
        public string? ChromiumExecutablePath { get; set; }
    }
}
