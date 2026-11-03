using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;
using EINVWORLD.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>FINDINGS.md #19: admin account changes reach the tamper-evident audit chain.</summary>
    public class AuditCoverageTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public AuditCoverageTests(SqlServerFixture fx) => _fx = fx;

        private sealed class RecordingAudit : IAuditService
        {
            public readonly List<(string Action, AuditEntry Entry)> Rows = new();
            public Task<bool> WriteAsync(string action, AuditEntry entry, CancellationToken ct = default) { Rows.Add((action, entry)); return Task.FromResult(true); }
            public Task<AuditVerificationResult> VerifyChainAsync(CancellationToken ct = default) => Task.FromResult(new AuditVerificationResult(true, 0, null, ""));
        }

        [Fact]
        public async Task Approving_Disapproving_And_Deleting_A_User_Are_Audited()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(ctx), Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(), null!, null!, null!, null!, null!, NullLogger<UserManager<ApplicationUser>>.Instance);
            var targetId = await CreateUserAsync(ctx, "audit-target");
            var audit = new RecordingAudit();
            var adminId = await CreateUserAsync(ctx, "audit-admin");
            var page = new eInvWorld.Pages.Admin.Users.ManageUserModel(users, null!, audit)
            {
                PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = PrincipalFor(adminId, "audit-admin", "Admin") } }
            };

            await page.OnPostApproveAsync(targetId, 1, "", "", "", "");
            var stampBefore = (await users.FindByIdAsync(targetId))!.SecurityStamp;
            await page.OnPostDisapproveAsync(targetId, 1, "", "", "", "");
            ctx.ChangeTracker.Clear();
            Assert.NotEqual(stampBefore, (await users.FindByIdAsync(targetId))!.SecurityStamp); // their sessions end
            await page.OnPostDeleteAsync(targetId, 1, "", "", "", "");

            Assert.Equal(new[] { "UserApproved", "UserDisapproved", "UserDeleted" }, audit.Rows.ConvertAll(r => r.Action));
            Assert.All(audit.Rows, r => Assert.Contains(targetId, r.Entry.NewValueJson));
        }
    }
}
