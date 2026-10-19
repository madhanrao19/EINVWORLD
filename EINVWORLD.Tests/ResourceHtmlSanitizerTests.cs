using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    public class ResourceHtmlSanitizerTests
    {
        [Fact]
        public void RemovesScriptsAndEventHandlers()
        {
            var html = ResourceHtmlSanitizer.Sanitize(
                "<p onclick=\"alert(1)\">Hi</p><script>alert(2)</script><img src=\"x\" onerror=\"alert(3)\"><a href=\"javascript:alert(4)\">x</a>");

            Assert.DoesNotContain("script", html);
            Assert.DoesNotContain("onclick", html);
            Assert.DoesNotContain("onerror", html);
            Assert.DoesNotContain("javascript:", html);
            Assert.Contains("Hi", html);
        }

        [Fact]
        public void StripsWordFormattingButKeepsTextAndStructure()
        {
            var word = "<!--[if gte mso 9]><xml>x</xml><![endif]-->" +
                       "<h1 class=\"MsoTitle\" style=\"background:white;mso-line-height-rule:exactly;font-size:28pt;text-align:center\">" +
                       "<span style=\"font-family:Calibri;color:black\">Title</span></h1>" +
                       "<p class=\"MsoNormal\"><font face=\"Arial\" size=\"2\">Body<o:p></o:p></font></p>";

            var html = ResourceHtmlSanitizer.Sanitize(word);

            Assert.DoesNotContain("background", html);
            Assert.DoesNotContain("font-size", html);
            Assert.DoesNotContain("font-family", html);
            Assert.DoesNotContain("mso", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<font", html);
            Assert.DoesNotContain("o:p", html);
            Assert.Contains("text-align: center", html);
            Assert.Contains("<h1", html);
            Assert.Contains("Title", html);
            Assert.Contains("Body", html);
        }

        [Fact]
        public void KeepsArticleElements()
        {
            var input = "<h2>Q</h2><ul><li>a</li></ul><table><tbody><tr><td>1</td></tr></tbody></table>" +
                        "<a href=\"https://einvworld.com\" target=\"_blank\" rel=\"noopener\">link</a><img src=\"/uploads/a.png\" alt=\"a\">";

            var html = ResourceHtmlSanitizer.Sanitize(input);

            Assert.Contains("<table>", html);
            Assert.Contains("<li>a</li>", html);
            Assert.Contains("href=\"https://einvworld.com\"", html);
            Assert.Contains("rel=\"noopener\"", html);
            Assert.Contains("src=\"/uploads/a.png\"", html);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void BlankInputReturnsEmpty(string? input) => Assert.Equal(string.Empty, ResourceHtmlSanitizer.Sanitize(input));
    }
}
