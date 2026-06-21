using System.Security.Claims;
using System.Text.Json;
using eInvWorld.Data;
using EINVWORLD.Services.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Assistant
{
    [Authorize(Roles = "Admin,Supplier")]
    public class IndexModel : PageModel
    {
        private const int MaxHistoryTurns = 24;

        private readonly IEInvoiceAssistantService _assistant;
        private readonly ApplicationDbContext _context;

        public IndexModel(IEInvoiceAssistantService assistant, ApplicationDbContext context)
        {
            _assistant = assistant;
            _context = context;
        }

        public bool Enabled => _assistant.IsEnabled;

        [BindProperty]
        public string? Question { get; set; }

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public string? RejectionText { get; set; }

        /// <summary>Carries the running conversation across posts so the Q&amp;A keeps context.</summary>
        [BindProperty]
        public string? ChatHistoryJson { get; set; }

        public List<ChatTurn> Conversation { get; private set; } = new();
        public string? SuggestionJson { get; private set; }
        public SuggestionReview? Review { get; private set; }
        public string? RejectionExplanation { get; private set; }
        public string? ErrorText { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAskAsync(CancellationToken ct)
        {
            var history = ParseHistory(ChatHistoryJson);
            var question = (Question ?? string.Empty).Trim();

            var result = await _assistant.AskAsync(history, question, ct);
            if (result.Ok)
            {
                history.Add(new ChatTurn("user", question));
                history.Add(new ChatTurn("assistant", result.Content));
                if (history.Count > MaxHistoryTurns)
                    history = history.Skip(history.Count - MaxHistoryTurns).ToList();
                Question = null; // clear the input box after a successful ask
            }
            else
            {
                ErrorText = result.Error;
            }

            Conversation = history;
            ChatHistoryJson = Serialize(history);
            return Page();
        }

        public IActionResult OnPostNewChat()
        {
            // Start a fresh conversation.
            Conversation = new();
            ChatHistoryJson = null;
            Question = null;
            return Page();
        }

        public async Task<IActionResult> OnPostSuggestAsync(CancellationToken ct)
        {
            // Preserve any ongoing conversation across this post.
            Conversation = ParseHistory(ChatHistoryJson);

            // Ground the suggestion on the current user's real customers so the model picks an existing
            // buyer + exact TIN instead of inventing one.
            var knownBuyers = await LoadKnownBuyersAsync(ct);

            var result = await _assistant.SuggestInvoiceAsync(Description ?? string.Empty, knownBuyers, ct);
            if (result.Ok)
            {
                SuggestionJson = result.Content;
                // Validate the model's output against real LHDN codes + the user's customers before they act on it.
                Review = _assistant.ReviewSuggestion(result.Content, knownBuyers.Select(b => b.Tin).ToList());
            }
            else
            {
                ErrorText = result.Error;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostExplainRejectionAsync(CancellationToken ct)
        {
            Conversation = ParseHistory(ChatHistoryJson);

            var result = await _assistant.ExplainRejectionAsync(RejectionText ?? string.Empty, ct);
            if (result.Ok) RejectionExplanation = result.Content;
            else ErrorText = result.Error;
            return Page();
        }

        /// <summary>The customers the current user is allowed to invoice (PartyInfos linked via SupplierBuyers).</summary>
        private async Task<List<KnownBuyer>> LoadKnownBuyersAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return new();

            var companyIds = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfoId)
                .ToListAsync(ct);
            if (companyIds.Count == 0) return new();

            var rows = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.BuyerId == p.PartyInfoId))
                .Select(p => new { p.CompanyName, p.TIN })
                .Take(200)
                .ToListAsync(ct);

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.TIN))
                .Select(r => new KnownBuyer(r.CompanyName ?? string.Empty, r.TIN!))
                .ToList();
        }

        private static List<ChatTurn> ParseHistory(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<ChatTurn>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static string Serialize(List<ChatTurn> history) => JsonSerializer.Serialize(history);
    }
}
