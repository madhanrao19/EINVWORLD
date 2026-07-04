using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Background;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Webhooks;
using eInvWorld.Services.Webhooks;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eInvWorld.Pages.Admin
{
    /// <summary>
    /// Admin management of outbound webhook subscriptions: register / edit / enable-disable / rotate-secret
    /// / delete / send-test. The signing secret is generated server-side and shown exactly once (on create
    /// or rotate); it is stored encrypted and never rendered again. All mutating actions are audited.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class WebhooksModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;
        private readonly ISyncJobTracker _jobs;
        private readonly WebhookSettings _settings;

        public WebhooksModel(ApplicationDbContext db, IAuditService audit, ISyncJobTracker jobs,
            IOptions<WebhookSettings> settings)
        {
            _db = db;
            _audit = audit;
            _jobs = jobs;
            _settings = settings.Value;
        }

        public List<WebhookSubscription> Subscriptions { get; private set; } = new();
        public bool WebhooksEnabled => _settings.Enabled;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData] public string? StatusMessage { get; set; }
        [TempData] public bool StatusIsError { get; set; }
        /// <summary>Set once after create/rotate so the plaintext secret can be shown a single time.</summary>
        [TempData] public string? RevealedSecret { get; set; }
        [TempData] public string? RevealedFor { get; set; }

        public sealed class InputModel
        {
            [Required, StringLength(50)]
            [Display(Name = "Company TIN")]
            public string Tin { get; set; } = string.Empty;

            [Required, StringLength(2048)]
            [Url(ErrorMessage = "Enter a valid absolute URL.")]
            [Display(Name = "Callback URL")]
            public string CallbackUrl { get; set; } = string.Empty;

            [StringLength(200)]
            public string? Description { get; set; }
        }

        public async Task OnGetAsync()
        {
            Subscriptions = await _db.WebhookSubscriptions.AsNoTracking()
                .OrderBy(s => s.Tin).ThenByDescending(s => s.Id)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var secret = GenerateSecret();
            var sub = new WebhookSubscription
            {
                Tin = Input.Tin.Trim(),
                CallbackUrl = Input.CallbackUrl.Trim(),
                Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
                Secret = secret,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name
            };
            _db.WebhookSubscriptions.Add(sub);
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("WebhookSubscriptionCreated", new AuditEntry
            {
                Tin = sub.Tin,
                NewValueJson = $"{{\"id\":{sub.Id},\"url\":{System.Text.Json.JsonSerializer.Serialize(sub.CallbackUrl)}}}"
            });

            RevealedSecret = secret;
            RevealedFor = $"{sub.Tin} (#{sub.Id})";
            StatusMessage = "Webhook subscription created. Copy the signing secret now — it is shown only once.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRotateAsync(int id)
        {
            var sub = await _db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id);
            if (sub is null) return NotFoundMessage();

            var secret = GenerateSecret();
            sub.Secret = secret;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("WebhookSecretRotated", new AuditEntry
            {
                Tin = sub.Tin, NewValueJson = $"{{\"id\":{sub.Id}}}"
            });

            RevealedSecret = secret;
            RevealedFor = $"{sub.Tin} (#{sub.Id})";
            StatusMessage = "Signing secret rotated. Copy the new secret now — it is shown only once. Update the receiver before the next event.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(int id)
        {
            var sub = await _db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id);
            if (sub is null) return NotFoundMessage();

            sub.IsEnabled = !sub.IsEnabled;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync(sub.IsEnabled ? "WebhookSubscriptionEnabled" : "WebhookSubscriptionDisabled",
                new AuditEntry { Tin = sub.Tin, NewValueJson = $"{{\"id\":{sub.Id}}}" });

            StatusMessage = $"Subscription #{sub.Id} {(sub.IsEnabled ? "enabled" : "disabled")}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var sub = await _db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id);
            if (sub is null) return NotFoundMessage();

            _db.WebhookSubscriptions.Remove(sub);
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("WebhookSubscriptionDeleted",
                new AuditEntry { Tin = sub.Tin, NewValueJson = $"{{\"id\":{id}}}" });

            StatusMessage = $"Subscription #{id} deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTestAsync(int id)
        {
            var sub = await _db.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (sub is null) return NotFoundMessage();

            // Enqueue a real WebhookDelivery job with synthetic invoice details so it exercises the full
            // signing + delivery + retry path and shows up in Admin → Sync Jobs.
            var jobId = await _jobs.CreateAsync(sub.Tin, SyncJobType.WebhookDelivery, User.Identity?.Name ?? "Admin",
                SyncJobPayload.CreateForWebhook(sub.Id, "TEST-WEBHOOK", "Valid", Guid.NewGuid().ToString("N")));

            await _audit.WriteAsync("WebhookTestSent", new AuditEntry { Tin = sub.Tin, NewValueJson = $"{{\"id\":{sub.Id},\"jobId\":{jobId}}}" });

            StatusMessage = jobId > 0
                ? $"Test event queued for subscription #{sub.Id} (job #{jobId}). Check the receiver and Admin → Sync Jobs for the result."
                : $"Could not queue a test event for subscription #{sub.Id} (is the Sync Jobs table present?).";
            StatusIsError = jobId <= 0;
            return RedirectToPage();
        }

        private IActionResult NotFoundMessage()
        {
            StatusMessage = "That subscription no longer exists.";
            StatusIsError = true;
            return RedirectToPage();
        }

        /// <summary>256-bit URL-safe random secret.</summary>
        private static string GenerateSecret() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
