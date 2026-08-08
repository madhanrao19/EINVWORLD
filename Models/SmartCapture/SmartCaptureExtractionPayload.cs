using System.Collections.Generic;

namespace eInvWorld.Models.SmartCapture
{
    /// <summary>Wire shape stored in SmartCaptureDocument.NormalizedExtractionJson — the raw suggestion JSON
    /// (as produced by IEInvoiceAssistantService.SuggestInvoiceAsync) plus the structured review checklist
    /// (from InvoiceSuggestionValidator.Review), so the review page can render both without re-running
    /// extraction.</summary>
    public sealed class SmartCaptureExtractionPayload
    {
        public string? SuggestionJson { get; set; }
        public List<SmartCaptureReviewItemDto> ReviewItems { get; set; } = new();
        public bool ReviewHasErrors { get; set; }
        public bool ReviewReadyForForm { get; set; }
    }

    /// <summary>Serializable mirror of EINVWORLD.Services.Assistant.CheckItem (a record with an enum member,
    /// kept as a plain DTO here so the stored JSON shape doesn't depend on the assistant's internal type).</summary>
    public sealed record SmartCaptureReviewItemDto(string Severity, string Message);
}
