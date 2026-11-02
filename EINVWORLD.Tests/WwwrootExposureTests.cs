using System.IO;
using System.Linq;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Everything under wwwroot is served to anyone without signing in (UseStaticFiles runs before
    /// authentication). Invoice data and debug pages must never live there: v1.50.18 still shipped 2024 draft
    /// invoices (with an NRIC, TIN, email and phone) and sample upload CSVs that Staging served publicly.
    /// </summary>
    public class WwwrootExposureTests
    {
        private static string WebRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir); // the test runs from inside the repo
            return Path.Combine(dir!.FullName, "wwwroot");
        }

        [Theory]
        [InlineData("Drafts")]   // invoice draft JSON: the app writes these to FilePathConfig:DraftFolder, outside wwwroot
        [InlineData("tempcsv")]  // uploaded CSVs
        public void NoInvoiceDataFolderUnderWwwroot(string folder)
        {
            var path = Path.Combine(WebRoot(), folder);
            Assert.False(Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any(),
                $"wwwroot/{folder} holds files, and anything under wwwroot is public.");
        }

        [Fact]
        public void NoDebugPagesUnderWwwroot()
        {
            var debugPages = Directory.EnumerateFiles(WebRoot(), "*.html", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => n!.StartsWith("test-") || n.StartsWith("theme-test"))
                .ToList();
            Assert.Empty(debugPages);
        }
    }
}
