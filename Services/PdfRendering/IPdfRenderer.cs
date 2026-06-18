namespace eInvWorld.Services.PdfRendering
{
    /// <summary>Renders an HTML string to PDF bytes. Implementations: DinkToPdf (wkhtmltopdf) and Puppeteer (Chromium).</summary>
    public interface IPdfRenderer
    {
        Task<byte[]> RenderHtmlToPdfAsync(string htmlContent);
    }
}
