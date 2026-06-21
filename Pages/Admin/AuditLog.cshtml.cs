using eInvWorld.Data;
using eInvWorld.Models.Audit;
using EINVWORLD.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditLogModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public AuditLogModel(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public List<AuditLog> Entries { get; private set; } = new();
        public AuditVerificationResult? Verification { get; private set; }

        public async Task OnGetAsync()
        {
            Entries = await _db.Set<AuditLog>()
                .AsNoTracking()
                .OrderByDescending(a => a.Id)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostVerifyAsync()
        {
            Verification = await _audit.VerifyChainAsync();
            await OnGetAsync();
            return Page();
        }
    }
}
