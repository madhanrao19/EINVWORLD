using System;
using System.Collections.Generic;
using System.Linq;
using eInvWorld.Models.Document;
using Newtonsoft.Json.Linq;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Turns LHDN's validation output into what the invoice screens show. Two shapes come back from MyInvois:
    /// the per-step <c>validationResults</c> of GET /documents/{uuid}/details, and the <c>rejectedDocuments</c>
    /// of a POST /documentsubmissions that LHDN refused outright. Both are parsed defensively: from the
    /// 24 Oct 2026 SDK release the errors may carry SDK error-page references, so any extra field LHDN adds
    /// whose value is a link is kept as a reference instead of being silently dropped.
    /// </summary>
    public static class LhdnValidationErrors
    {
        /// <summary>One row of the validation-details popup (shape stored in InvoiceHeader.LHDNValidationErrorJson).</summary>
        public sealed record Error(string? step, string? errorCode, string? error, string? propertyName, string? propertyPath,
            List<string> references, List<InnerError> innerErrors);

        /// <summary>A nested error; deeper levels are flattened into the same list.</summary>
        public sealed record InnerError(string? errorCode, string? propertyName, string? propertyPath, string? error, List<string> references);

        /// <summary>The failed steps (status Invalid/Error, any casing) of a document's validation results.</summary>
        public static List<Error> Extract(ValidationResults? results)
        {
            var list = new List<Error>();
            foreach (var step in results?.validationSteps ?? Enumerable.Empty<ValidationStep>())
            {
                if (step?.error == null) continue;
                if (!string.Equals(step.status, "Invalid", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(step.status, "Error", StringComparison.OrdinalIgnoreCase)) continue;

                var e = step.error;
                var inner = new List<InnerError>();
                Flatten(e.innerError, inner, depth: 0);
                list.Add(new Error(step.name, e.errorCode, e.error, e.propertyName, e.propertyPath, Links(e.Extra), inner));
            }
            return list;
        }

        private static void Flatten(List<ValidationInnerError>? errors, List<InnerError> into, int depth)
        {
            if (errors == null || depth > 5) return; // LHDN nests at most a level or two; cap guards a malformed body
            foreach (var ie in errors.Where(x => x != null))
            {
                into.Add(new InnerError(ie.errorCode, ie.propertyName, ie.propertyPath, ie.error, Links(ie.Extra)));
                Flatten(ie.innerError, into, depth + 1);
            }
        }

        /// <summary>Values of unknown fields that are web links (e.g. a future "errorReference" field).</summary>
        private static List<string> Links(IDictionary<string, JToken>? extra) =>
            extra?.Values
                .Where(v => v.Type == JTokenType.String)
                .Select(v => v.Value<string>()!.Trim())
                .Where(s => s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList() ?? new List<string>();

        /// <summary>
        /// Plain-text summary of a submission's <c>rejectedDocuments</c>, one line per document and detail, e.g.
        /// <c>EINV100581: Validation Error. Buyer TIN is invalid (at Invoice.AccountingCustomerParty...)</c>.
        /// Falls back to the raw body when it can't be read, so nothing LHDN said is lost.
        /// </summary>
        public static string DescribeRejection(string? responseContent)
        {
            if (string.IsNullOrWhiteSpace(responseContent)) return "LHDN gave no details.";
            try
            {
                var rejected = JObject.Parse(responseContent)["rejectedDocuments"] as JArray;
                if (rejected == null || rejected.Count == 0) return responseContent;

                var lines = new List<string>();
                foreach (var doc in rejected)
                {
                    var no = (string?)doc["invoiceCodeNumber"];
                    var err = doc["error"];
                    var head = Text(err?["message"]) ?? Text(err?["code"]) ?? "Rejected";
                    var details = (err?["details"] as JArray)?.Select(DetailText).Where(s => s != null).ToList();
                    lines.Add($"{(string.IsNullOrEmpty(no) ? "" : no + ": ")}{head}"
                        + (details is { Count: > 0 } ? " " + string.Join(" ", details) : ""));
                }
                return string.Join(Environment.NewLine, lines);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return responseContent;
            }
        }

        private static string? DetailText(JToken d)
        {
            var message = Text(d["message"]);
            if (message == null) return null;
            var at = Text(d["propertyPath"]) ?? Text(d["target"]);
            var code = Text(d["code"]);
            return (code != null ? $"[{code}] " : "") + message.TrimEnd('.') + (at != null ? $" (at {at})." : ".");
        }

        private static string? Text(JToken? t) =>
            t == null || t.Type is JTokenType.Null or JTokenType.Undefined ? null
            : t.Type == JTokenType.String ? ((string?)t is { Length: > 0 } s ? s : null)
            : t.ToString(Newtonsoft.Json.Formatting.None);
    }
}
