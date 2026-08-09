using System.Text.Json;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.SmartCapture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Admin
{
    /// <summary>
    /// Smart Capture Stage 4: the ONLY place a company's automatic-LHDN-submission opt-in can be turned
    /// on. Deliberately system-Admin-only (not self-service on the Supplier company workspace) — a company
    /// cannot enable unattended submission to a government tax authority for itself without EINVWORLD
    /// operator involvement. Even then, this is only the per-company half of the gate: SmartCaptureOptions
    /// .AutoSubmitEnabled (config, default false) is a separate global kill switch that must ALSO be true,
    /// and SmartCaptureAutoSubmitEligibilityService still re-checks every condition (doc type, zero review
    /// issues, exact buyer match, value ceiling) on every single confirmation — this page only sets the
    /// company-level policy, never bypasses the per-document checks.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class SmartCaptureAutoSubmitModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<eInvWorld.Models.ApplicationUser> _userManager;
        private readonly IAuditService _audit;
        private readonly SmartCaptureAutoSubmitEligibilityService _autoSubmit;

        public SmartCaptureAutoSubmitModel(
            ApplicationDbContext context, UserManager<eInvWorld.Models.ApplicationUser> userManager, IAuditService audit,
            SmartCaptureAutoSubmitEligibilityService autoSubmit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
            _autoSubmit = autoSubmit;
        }

        public List<(int PartyInfoId, string Name, bool Enabled)> Companies { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? CompanyPartyInfoId { get; set; }

        public PartyInfo? SelectedCompany { get; private set; }

        [BindProperty]
        public bool Enabled { get; set; }

        [BindProperty]
        public string AllowedDocTypesCsv { get; set; } = "01";

        [BindProperty]
        public decimal MaxAutoSubmitValue { get; set; }

        [BindProperty]
        public int DelayMinutes { get; set; } = 20;

        public string? ErrorText { get; private set; }
        public string? SuccessText { get; private set; }

        public async Task OnGetAsync(CancellationToken ct)
        {
            await LoadCompaniesAsync(ct);
            if (CompanyPartyInfoId is int id)
                await LoadSelectedAsync(id, ct);
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            await LoadCompaniesAsync(ct);

            if (CompanyPartyInfoId is not int id)
            {
                ErrorText = "Please choose a company.";
                return Page();
            }

            SelectedCompany = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id, ct);
            if (SelectedCompany is null) return NotFound();

            if (Enabled && MaxAutoSubmitValue <= 0)
            {
                ErrorText = "A value ceiling greater than 0 is required to enable automatic submission — there is no unlimited tier.";
                return Page();
            }

            var settings = await _context.SmartCaptureAutoSubmitSettings.FirstOrDefaultAsync(s => s.CompanyPartyInfoId == id, ct);
            if (settings is null)
            {
                settings = new SmartCaptureAutoSubmitSettings { CompanyPartyInfoId = id };
                _context.SmartCaptureAutoSubmitSettings.Add(settings);
            }

            var wasEnabled = settings.Enabled;
            settings.Enabled = Enabled;
            settings.AllowedDocTypesCsv = string.IsNullOrWhiteSpace(AllowedDocTypesCsv) ? "01" : AllowedDocTypesCsv.Trim();
            settings.MaxAutoSubmitValue = MaxAutoSubmitValue;
            settings.DelayMinutes = DelayMinutes < 1 ? 1 : DelayMinutes;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedByUserId = _userManager.GetUserId(User);
            await _context.SaveChangesAsync(ct);

            await _audit.WriteAsync("SmartCaptureAutoSubmitSettingsChanged", new AuditEntry
            {
                NewValueJson = JsonSerializer.Serialize(new
                {
                    companyPartyInfoId = id,
                    wasEnabled,
                    nowEnabled = settings.Enabled,
                    settings.AllowedDocTypesCsv,
                    settings.MaxAutoSubmitValue,
                    settings.DelayMinutes,
                })
            }, ct);

            SuccessText = "Settings saved.";

            // Turning the toggle off should act like a stop, not just a "no new schedulings" switch —
            // retract any jobs already scheduled during their delay window for this company.
            if (wasEnabled && !Enabled)
            {
                var cancelled = await _autoSubmit.CancelAllPendingForCompanyAsync(id, ct);
                if (cancelled > 0)
                    SuccessText += $" {cancelled} already-scheduled auto-submission(s) for this company were also cancelled.";
            }

            await LoadSelectedAsync(id, ct);
            return Page();
        }

        private async Task LoadCompaniesAsync(CancellationToken ct)
        {
            var settingsByCompany = await _context.SmartCaptureAutoSubmitSettings
                .Select(s => new { s.CompanyPartyInfoId, s.Enabled })
                .ToListAsync(ct);
            var enabledSet = settingsByCompany.Where(s => s.Enabled).Select(s => s.CompanyPartyInfoId).ToHashSet();

            var rows = await _context.PartyInfos
                .Where(p => p.TIN != null && p.TIN != "")
                .OrderBy(p => p.CompanyName)
                .Select(p => new { p.PartyInfoId, p.CompanyName })
                .Take(500)
                .ToListAsync(ct);
            Companies = rows.Select(p => (p.PartyInfoId, p.CompanyName ?? $"#{p.PartyInfoId}", enabledSet.Contains(p.PartyInfoId))).ToList();
        }

        private async Task LoadSelectedAsync(int id, CancellationToken ct)
        {
            SelectedCompany = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id, ct);
            if (SelectedCompany is null) return;

            var settings = await _context.SmartCaptureAutoSubmitSettings.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyPartyInfoId == id, ct);
            Enabled = settings?.Enabled ?? false;
            AllowedDocTypesCsv = settings?.AllowedDocTypesCsv ?? "01";
            MaxAutoSubmitValue = settings?.MaxAutoSubmitValue ?? 0;
            DelayMinutes = settings?.DelayMinutes ?? 20;
        }
    }
}
