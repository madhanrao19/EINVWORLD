using DinkToPdf;
using DinkToPdf.Contracts;

namespace eInvWorld.Services.PdfRendering
{
    /// <summary>
    /// Default renderer — wkhtmltopdf via DinkToPdf. Preserves the exact output settings the app
    /// has always used (A4 portrait, 15/15/10/10 margins, centered "Page X of Y" footer).
    /// </summary>
    public class DinkToPdfRenderer : IPdfRenderer
    {
        private readonly IConverter _converter;

        public DinkToPdfRenderer(IConverter converter)
        {
            _converter = converter;
        }

        public Task<byte[]> RenderHtmlToPdfAsync(string htmlContent)
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

            // DinkToPdf's Convert returns the PDF bytes when no Out path is set.
            return Task.FromResult(_converter.Convert(doc));
        }
    }
}
