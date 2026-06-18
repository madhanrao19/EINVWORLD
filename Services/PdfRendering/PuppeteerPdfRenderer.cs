using eInvWorld.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace eInvWorld.Services.PdfRendering
{
    /// <summary>
    /// Headless-Chromium renderer via PuppeteerSharp (MIT, maintained, no native DLL). Reproduces the
    /// DinkToPdf layout: A4 portrait, 15/15/10/10 mm margins, centered "Page X of Y" footer, backgrounds on.
    /// </summary>
    public class PuppeteerPdfRenderer : IPdfRenderer
    {
        private readonly PDFGenerationSettings _settings;
        private readonly ILogger<PuppeteerPdfRenderer> _logger;

        private static readonly SemaphoreSlim _browserInit = new(1, 1);
        private static bool _browserReady;

        public PuppeteerPdfRenderer(IOptions<PDFGenerationSettings> settings, ILogger<PuppeteerPdfRenderer> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<byte[]> RenderHtmlToPdfAsync(string htmlContent)
        {
            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            };

            if (!string.IsNullOrWhiteSpace(_settings.ChromiumExecutablePath))
            {
                launchOptions.ExecutablePath = _settings.ChromiumExecutablePath;
            }
            else
            {
                await EnsureBrowserDownloadedAsync();
            }

            await using var browser = await Puppeteer.LaunchAsync(launchOptions);
            await using var page = await browser.NewPageAsync();

            await page.SetContentAsync(htmlContent, new SetContentOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Load }
            });

            // Give images/fonts a moment to settle before printing (configurable).
            if (_settings.PlaywrightRenderDelayMs > 0)
                await Task.Delay(_settings.PlaywrightRenderDelayMs);

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "15mm", Bottom = "15mm", Left = "10mm", Right = "10mm" },
                DisplayHeaderFooter = true,
                HeaderTemplate = "<div></div>",
                FooterTemplate = "<div style=\"font-size:8px; width:100%; text-align:center; color:#444;\">" +
                                 "Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>"
            });
        }

        private async Task EnsureBrowserDownloadedAsync()
        {
            if (_browserReady) return;
            await _browserInit.WaitAsync();
            try
            {
                if (_browserReady) return;
                _logger.LogInformation("⬇️ Ensuring a Chromium build is available for PuppeteerSharp (first run only)...");
                await new BrowserFetcher().DownloadAsync();
                _browserReady = true;
            }
            finally
            {
                _browserInit.Release();
            }
        }
    }
}
