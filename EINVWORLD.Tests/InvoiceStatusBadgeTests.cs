using System.IO;
using System.Text.Encodings.Web;
using eInvWorld.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    public class InvoiceStatusBadgeTests
    {
        [Theory]
        [InlineData("Valid", "valid")]
        [InlineData("Completed", "valid")]
        [InlineData("Submitted", "submitted")]
        [InlineData("ValidInternal", "submitted")]
        [InlineData("Invalid", "invalid")]
        [InlineData("TransmissionError", "invalid")]
        [InlineData("RequestReject", "rejected")]
        [InlineData("Cancelled", "cancelled")]
        [InlineData("CancelledInternal", "cancelled")]
        [InlineData("Draft", "draft")]
        [InlineData(null, "draft")]
        [InlineData("SomethingNew", "draft")]
        public void Tone_MapsEveryStatus(string? statusId, string tone)
        {
            Assert.Equal(tone, InvoiceStatusBadge.Tone(statusId));
            Assert.Equal("einv-badge einv-status-" + tone, InvoiceStatusBadge.CssClass(statusId));
        }

        [Theory]
        [InlineData("RequestReject", "Request Reject")] // enum description
        [InlineData("CancelledInternal", "Cancelled")]
        [InlineData("SomethingNew", "SomethingNew")]   // unknown id shown as-is
        [InlineData(null, "")]
        public void Label_UsesStatusDescription(string? statusId, string label)
        {
            Assert.Equal(label, InvoiceStatusBadge.Label(statusId));
        }

        [Fact]
        public void Render_EncodesLabelAndAddsIcon()
        {
            using var w = new StringWriter();
            InvoiceStatusBadge.Render("Valid", "<b>Valid</b>").WriteTo(w, HtmlEncoder.Default);
            Assert.Equal(
                "<span class=\"einv-badge einv-status-valid\"><i class=\"ri-checkbox-circle-line\" aria-hidden=\"true\"></i>&lt;b&gt;Valid&lt;/b&gt;</span>",
                w.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Render_NoStatusShowsDash(string? statusId)
        {
            using var w = new StringWriter();
            InvoiceStatusBadge.Render(statusId).WriteTo(w, HtmlEncoder.Default);
            Assert.Equal("<span class=\"text-muted\" aria-label=\"No status yet\">&mdash;</span>", w.ToString());
        }

        [Fact]
        public void Render_NeutralStatusHasNoIcon()
        {
            using var w = new StringWriter();
            InvoiceStatusBadge.Render("Draft").WriteTo(w, HtmlEncoder.Default);
            Assert.Equal("<span class=\"einv-badge einv-status-draft\">Draft</span>", w.ToString());
        }
    }
}
