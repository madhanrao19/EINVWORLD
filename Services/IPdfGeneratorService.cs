namespace eInvWorld.Services
{
    /// <summary>Generates invoice PDFs (via the configured <see cref="PdfRendering.IPdfRenderer"/>).
    /// Behind an interface so consumers depend on the abstraction (DIP) and it can be mocked in tests.</summary>
    public interface IPdfGeneratorService
    {
        Task<string> GeneratePdfFromHtmlAsync(string invoiceNo, string htmlContent);
        Task<string> GeneratePdfAsync(string invoiceNo);
    }
}
