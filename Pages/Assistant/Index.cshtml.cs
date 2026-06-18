using EINVWORLD.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Pages.Assistant
{
    [Authorize(Roles = "Admin,Supplier")]
    public class IndexModel : PageModel
    {
        private readonly IEInvoiceAssistantService _assistant;

        public IndexModel(IEInvoiceAssistantService assistant) => _assistant = assistant;

        public bool Enabled => _assistant.IsEnabled;

        [BindProperty]
        public string? Question { get; set; }

        [BindProperty]
        public string? Description { get; set; }

        public string? AnswerText { get; private set; }
        public string? SuggestionJson { get; private set; }
        public string? ErrorText { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAskAsync(CancellationToken ct)
        {
            var result = await _assistant.AskAsync(Question ?? string.Empty, ct);
            if (result.Ok) AnswerText = result.Content;
            else ErrorText = result.Error;
            return Page();
        }

        public async Task<IActionResult> OnPostSuggestAsync(CancellationToken ct)
        {
            var result = await _assistant.SuggestInvoiceAsync(Description ?? string.Empty, ct);
            if (result.Ok) SuggestionJson = result.Content;
            else ErrorText = result.Error;
            return Page();
        }
    }
}
