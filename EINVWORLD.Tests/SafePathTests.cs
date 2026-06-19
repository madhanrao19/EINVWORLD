using System.IO;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Guards the path-traversal protection used by the public resource-serving endpoints
    /// (ResourcesApiController). These are the checks that stop requests such as
    /// "/api/resources/editor/..%2f..%2fweb.config" from reading files outside the base folder.
    /// </summary>
    public class SafePathTests
    {
        private static readonly string BaseFolder =
            Path.Combine(Path.GetTempPath(), "einvworld-resources");

        [Theory]
        [InlineData("logo.png")]
        [InlineData("company-123.jpg")]
        [InlineData("a.b.c.webp")]
        public void TryResolve_AllowsPlainFileNames(string fileName)
        {
            var ok = SafePath.TryResolve(BaseFolder, out var fullPath, fileName);

            Assert.True(ok);
            Assert.StartsWith(Path.GetFullPath(BaseFolder), fullPath);
            Assert.EndsWith(fileName, fullPath);
        }

        [Fact]
        public void TryResolve_AllowsNestedTrustedSegments()
        {
            var ok = SafePath.TryResolve(BaseFolder, out var fullPath, "invoices", "thumb", "x.png");

            Assert.True(ok);
            Assert.StartsWith(Path.GetFullPath(BaseFolder), fullPath);
        }

        [Theory]
        [InlineData("..")]
        [InlineData("..\\..\\web.config")]
        [InlineData("../../web.config")]
        [InlineData("foo/../../bar")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("")]
        [InlineData("   ")]
        public void TryResolve_RejectsTraversalAndSeparators(string segment)
        {
            var ok = SafePath.TryResolve(BaseFolder, out var fullPath, segment);

            Assert.False(ok);
            Assert.Equal(string.Empty, fullPath);
        }

        [Fact]
        public void TryResolve_RejectsTraversalInAnyOfMultipleSegments()
        {
            var ok = SafePath.TryResolve(BaseFolder, out _, "invoices", "..", "secret.txt");
            Assert.False(ok);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryResolve_RejectsMissingBaseFolder(string? baseFolder)
        {
            var ok = SafePath.TryResolve(baseFolder!, out var fullPath, "logo.png");

            Assert.False(ok);
            Assert.Equal(string.Empty, fullPath);
        }

        [Fact]
        public void TryResolve_RejectsNoSegments()
        {
            var ok = SafePath.TryResolve(BaseFolder, out var fullPath);

            Assert.False(ok);
            Assert.Equal(string.Empty, fullPath);
        }
    }
}
