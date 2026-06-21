using System.Security.Claims;
using System.Text.Json;
using eInvWorld.Data;
using eInvWorld.Helpers;
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
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IEInvoiceAssistantService assistant, ApplicationDbContext context, ILogger<IndexModel> logger)
        {
            _assistant = assistant;
            _context = context;
            _logger = logger;
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

                // Audit that the AI assistant was used to draft an invoice (best-effort; never blocks the feature).
                try
                {
                    var summary = (Description ?? string.Empty).Trim();
                    if (summary.Length > 500) summary = summary[..500];
                    await UserActivityLogger.LogAsync(_context, HttpContext,
                        action: "AI invoice suggestion generated", module: "AI Assistant", data: summary);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write AI-assistant activity log (non-fatal).");
                }
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

        /// <summary>
        /// The customers the current user is allowed to invoice — both registered buyers (PartyInfo) and
        /// public customers (PublicCustomer), linked via SupplierBuyers. Matches the buyer set the Create
        /// Invoice form offers, so the assistant grounds/validates against the same customers.
        /// </summary>
        private async Task<List<KnownBuyer>> LoadKnownBuyersAsync(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return new();

            var companyIds = await _context.UserCompanies
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.PartyInfoId)
                .ToListAsync(ct);
            if (companyIds.Count == 0) return new();

            // Registered buyers (PartyInfo) linked to the user's companies.
            var partyRows = await _context.PartyInfos
                .Where(p => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.BuyerId == p.PartyInfoId))
                .Select(p => new { p.CompanyName, p.TIN })
                .Take(200)
                .ToListAsync(ct);

            // Public customers linked to the user's companies (otherwise valid buyers would be unmatched).
            var publicRows = await _context.PublicCustomers
                .Where(pc => _context.SupplierBuyers.Any(sb => companyIds.Contains(sb.SupplierId) && sb.PublicCustomerId == pc.PublicCustomerId))
                .Select(pc => new { pc.CompanyName, pc.TIN })
                .Take(200)
                .ToListAsync(ct);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var buyers = new List<KnownBuyer>();

            void Add(string? name, string? tin)
            {
                if (string.IsNullOrWhiteSpace(tin) || !seen.Add(tin)) return;
                buyers.Add(new KnownBuyer(name ?? string.Empty, tin));
            }

            foreach (var r in partyRows) Add(r.CompanyName, r.TIN);
            foreach (var r in publicRows) Add(r.CompanyName, r.TIN);

            return buyers;
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
