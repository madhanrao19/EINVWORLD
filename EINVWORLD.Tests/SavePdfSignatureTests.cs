using System.IO;
using System.Text;
using System.Threading.Tasks;
using eInvWorld.Controllers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>/api/invoice/save-pdf keeps only real PDFs: the content must start with "%PDF-", whatever the name says.</summary>
    public class SavePdfSignatureTests
    {
        private static IFormFile File(string content)
        {
            var bytes = Encoding.ASCII.GetBytes(content);
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "EINV1_01012026_000000.pdf");
        }

        [Theory]
        [InlineData("%PDF-1.3\n%...", true)]
        [InlineData("<html><script>alert(1)</script></html>", false)]
        [InlineData("%PDF", false)]
        [InlineData("", false)]
        public async Task OnlyPdfSignatureIsAccepted(string content, bool expected) =>
            Assert.Equal(expected, await InvoiceController.LooksLikePdfAsync(File(content)));
    }
}
