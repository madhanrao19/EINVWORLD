using System.Linq;
using eInvWorld.Models.Document;
using EINVWORLD.Helpers;
using Newtonsoft.Json;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// LHDN validation output → what the invoice screens show. The samples follow the MyInvois SDK shapes;
    /// the "errorReference" field is a guess at the 24 Oct 2026 error-reference change and stands in for any
    /// field LHDN adds later — replace with a captured pre-production response once one is available.
    /// </summary>
    public class LhdnValidationErrorsTests
    {
        private const string DetailsBody = @"{
          ""status"": ""Invalid"",
          ""validationSteps"": [
            { ""status"": ""Valid"", ""name"": ""Step01-Structure Validator"" },
            { ""status"": ""invalid"", ""name"": ""Step03-Taxpayer Validator"",
              ""error"": {
                ""propertyName"": null, ""propertyPath"": null, ""errorCode"": ""CF321"",
                ""error"": ""Buyer TIN is invalid. Please refer to https://sdk.myinvois.hasil.gov.my/errors/CF321."",
                ""errorReference"": ""https://sdk.myinvois.hasil.gov.my/errors/CF321"",
                ""severity"": 2,
                ""innerError"": [
                  { ""propertyName"": ""BuyerTIN"", ""propertyPath"": ""Invoice.AccountingCustomerParty.Party.PartyIdentification"",
                    ""errorCode"": ""CF321"", ""error"": ""TIN not found"",
                    ""innerError"": [ { ""propertyName"": ""ID"", ""errorCode"": ""CF322"", ""error"": ""Nested detail"" } ] }
                ]
              } },
            { ""status"": ""Error"", ""name"": ""Step05-Code Validator"",
              ""error"": { ""errorCode"": ""CV301"", ""error"": ""Invalid classification code"", ""propertyPath"": ""Invoice.InvoiceLine[1]"" } }
          ]
        }";

        [Fact]
        public void Extract_Keeps_Only_Failed_Steps_With_Their_Names_References_And_All_Inner_Levels()
        {
            var results = JsonConvert.DeserializeObject<ValidationResults>(DetailsBody);
            var errors = LhdnValidationErrors.Extract(results);

            Assert.Equal(2, errors.Count); // Valid step skipped; "invalid" matched case-insensitively
            var tin = errors[0];
            Assert.Equal("Step03-Taxpayer Validator", tin.step);
            Assert.Equal("CF321", tin.errorCode);
            Assert.Contains("https://sdk.myinvois.hasil.gov.my/errors/CF321", tin.error);
            Assert.Equal(new[] { "https://sdk.myinvois.hasil.gov.my/errors/CF321" }, tin.references); // unknown field kept
            Assert.Equal(new[] { "CF321", "CF322" }, tin.innerErrors.Select(i => i.errorCode));     // second level flattened
            Assert.Equal("Invoice.AccountingCustomerParty.Party.PartyIdentification", tin.innerErrors[0].propertyPath);

            Assert.Equal("CV301", errors[1].errorCode);
            Assert.Empty(errors[1].references);
            Assert.Empty(errors[1].innerErrors);
        }

        [Fact]
        public void Extract_Handles_Missing_Results()
        {
            Assert.Empty(LhdnValidationErrors.Extract(null));
            Assert.Empty(LhdnValidationErrors.Extract(new ValidationResults()));
        }

        [Fact]
        public void Stored_Json_Keeps_The_Field_Names_The_Popup_Reads()
        {
            // InvoiceLists stores this as LHDNValidationErrorJson and the popup JS reads these names.
            var json = JsonConvert.SerializeObject(LhdnValidationErrors.Extract(JsonConvert.DeserializeObject<ValidationResults>(DetailsBody)));
            foreach (var name in new[] { "\"errorCode\"", "\"error\"", "\"propertyPath\"", "\"innerErrors\"", "\"propertyName\"", "\"step\"", "\"references\"" })
                Assert.Contains(name, json);
        }

        [Fact]
        public void DescribeRejection_Gives_One_Readable_Line_Per_Document()
        {
            const string body = @"{ ""submissionUid"": null, ""acceptedDocuments"": [],
              ""rejectedDocuments"": [ { ""invoiceCodeNumber"": ""EINV100581"",
                ""error"": { ""code"": ""ValidationError"", ""message"": ""Validation Error"", ""target"": ""EINV100581"",
                  ""details"": [ { ""code"": ""CF404"", ""message"": ""Enveloped document is missing. See https://sdk.myinvois.hasil.gov.my/errors/CF404"", ""target"": ""DocumentSignature"", ""propertyPath"": ""Invoice.UBLExtensions"" },
                                 { ""code"": null, ""message"": null } ] } } ] }";

            var text = LhdnValidationErrors.DescribeRejection(body);

            Assert.Equal("EINV100581: Validation Error [CF404] Enveloped document is missing. See https://sdk.myinvois.hasil.gov.my/errors/CF404 (at Invoice.UBLExtensions).", text);
            Assert.DoesNotContain("{", text);
        }

        [Fact]
        public void DescribeRejection_Falls_Back_To_The_Raw_Body_It_Cannot_Read()
        {
            Assert.Equal("<html>gateway</html>", LhdnValidationErrors.DescribeRejection("<html>gateway</html>"));
            Assert.Equal("LHDN gave no details.", LhdnValidationErrors.DescribeRejection(""));
        }
    }
}
