using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Create e-Invoice never saved a credit/debit/refund note's Reference UUID (Ready for LHDN then refused it, and
    /// before that check LHDN got "NA"). Guards the form posting one RefUUID field and every header the page builds
    /// carrying it.
    /// </summary>
    public class CreateInvoiceRefUuidTests
    {
        private static string Source(string relative)
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EINVWORLD.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(dir!.FullName, relative));
        }

        [Fact]
        public void Form_Posts_Exactly_One_RefUuid_Field()
        {
            // Two fields with the same name: the model binder takes the first, which was the empty display copy.
            var view = Source(@"Pages\Invoices\_CreateInvoice_Step1BasicInfo.cshtml");
            Assert.Single(Regex.Matches(view, @"(name|asp-for)=""Invoice\.RefUUID"""));
        }

        [Fact]
        public void Draft_Update_Draft_Create_And_Submit_Header_All_Carry_The_RefUuid()
        {
            var page = Source(@"Pages\Invoices\CreateInvoice.cshtml.cs");
            Assert.Contains("draftInvoice.RefUUID = Invoice.RefUUID;", page);                  // existing draft
            Assert.Equal(2, Regex.Matches(page, @"^\s*RefUUID = Invoice\.RefUUID,", RegexOptions.Multiline).Count); // new draft + submit header
            Assert.Contains("RefUUID = oldInvoice.RefUUID,", page);                            // copy / resend
        }
    }
}
