using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Assistant
{
    /// <summary>
    /// Read-only history of this user's AI-assisted activity — invoice suggestions (UserActivityLog,
    /// Module="AI Assistant") and document captures (AuditLog, Action in the DocumentCapture/Smart Capture
    /// set below). Admins see every user's activity; everyone else sees only their own. No AI provider
    /// secrets or raw extracted document content are ever stored in either log, so nothing here needs
    /// masking.
    /// </summary>
    [Authorize(Roles = "Admin,Supplier")]
    public class ProcessingHistoryModel : PageModel
    {
        /// <summary>Every AuditLog action written by the (synchronous) AI Document Capture flow and the
        /// (async) Smart Capture pipeline — kept as one list so this history page covers both.</summary>
        private static readonly string[] CaptureActions =
        {
            "DocumentCaptured",
            "SmartCaptureUploaded",
            "SmartCaptureExtracted",
            "SmartCaptureExtractionFailed",
            "SmartCaptureMalwareDetected",
            "SmartCaptureMalwareScanSkipped",
            "SmartCaptureDraftCreated",
            "SmartCaptureDocumentDownloaded",
        };

        private readonly ApplicationDbContext _context;
        private const int MaxRows = 50;

        public ProcessingHistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool IsAdmin { get; set; }
        public List<UserActivityLog> AssistantActivity { get; set; } = new();
        public List<AuditLog> DocumentCaptures { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IsAdmin = User.IsInRole("Admin");

            var activityQuery = _context.UserActivityLogs.Where(a => a.Module == "AI Assistant");
            var captureQuery = _context.Set<AuditLog>().Where(a => CaptureActions.Contains(a.Action));

            if (!IsAdmin)
            {
                activityQuery = activityQuery.Where(a => a.UserId == userId);
                captureQuery = captureQuery.Where(a => a.UserId == userId);
            }

            AssistantActivity = await activityQuery
                .OrderByDescending(a => a.Timestamp)
                .Take(MaxRows)
                .ToListAsync();

            DocumentCaptures = await captureQuery
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(MaxRows)
                .ToListAsync();
        }
    }
}
