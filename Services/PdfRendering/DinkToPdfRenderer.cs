using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Services.PdfRendering
{
    /// <summary>
    /// Default renderer — wkhtmltopdf via DinkToPdf. Preserves the exact output settings the app
    /// has always used (A4 portrait, 15/15/10/10 margins, centered "Page X of Y" footer).
    /// </summary>
    public class DinkToPdfRenderer : IPdfRenderer
    {
        private readonly IConverter _converter;
        private readonly int _timeoutSeconds;

        public DinkToPdfRenderer(IConverter converter, IConfiguration configuration)
        {
            _converter = converter;
            _timeoutSeconds = configuration.GetValue("PDFGenerationSettings:TimeoutSeconds", 60);
            if (_timeoutSeconds <= 0) _timeoutSeconds = 60;
        }

        public async Task<byte[]> RenderHtmlToPdfAsync(string htmlContent)
        {
            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = new GlobalSettings
                {
                    PaperSize = PaperKind.A4,
                    Orientation = Orientation.Portrait,
                    ColorMode = ColorMode.Color,
                    Margins = new MarginSettings { Top = 15, Bottom = 15, Left = 10, Right = 10 }
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = htmlContent,
                        WebSettings =
                        {
                            DefaultEncoding = "utf-8",
                            LoadImages = true,
                            PrintMediaType = true,
                            EnableIntelligentShrinking = false
                        },
                        FooterSettings = new FooterSettings
                        {
                            FontName = "Arial",
                            FontSize = 8,
                            Center = "Page [page] of [toPage]",
                            Line = false,
                            Spacing = 2.0
                        }
                    }
                }
            };

            // DinkToPdf's Convert is a synchronous native call (wkhtmltopdf) that can hang on malformed HTML
            // or an unreachable resource. Run it off the request thread with a timeout so a stuck render
            // returns a clear error instead of blocking the request indefinitely.
            // NOTE: the SynchronizedConverter serialises conversions on its own thread, so on timeout the
            // native work may keep running — this bounds the CALLER's wait, not the native process.
            var convertTask = Task.Run(() => _converter.Convert(doc));
            var finished = await Task.WhenAny(convertTask, Task.Delay(TimeSpan.FromSeconds(_timeoutSeconds)));
            if (finished != convertTask)
                throw new TimeoutException(
                    $"PDF generation exceeded {_timeoutSeconds}s and was abandoned. The HTML may be malformed or reference an unreachable resource.");

            return await convertTask; // observe the result (and surface any conversion exception)
        }
    }
}
