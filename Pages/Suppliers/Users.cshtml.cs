using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin,Supplier")]
    public class UsersModel : SupplierBasePage
    {
        private new readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICompanyAuthorizationService _companyAuth;
        private readonly ICompanyInvitationService _invitationService;
        private readonly EmailService _emailService;
        private readonly IAuditService _auditService;

        public UsersModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ICompanyAuthorizationService companyAuth, ICompanyInvitationService invitationService,
            EmailService emailService, IAuditService auditService) : base(context)
        {
            _context = context;
            _userManager = userManager;
            _companyAuth = companyAuth;
            _invitationService = invitationService;
            _emailService = emailService;
            _auditService = auditService;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public List<UserCompany> Members { get; set; } = new();
        public List<CompanyInvitation> PendingInvitations { get; set; } = new();
        public List<CompanyRole> AvailableRoles { get; set; } = new();
        public bool CanManageUsers { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; }

        [BindProperty]
        public string InviteEmail { get; set; } = string.Empty;

        [BindProperty]
        public int? InviteCompanyRoleId { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            PartyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == id) ?? null!;
            if (PartyInfo == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.PartyInfoId == id);
                if (!isMember) return Forbid();
            }

            CanManageUsers = isAdmin || (userId != null && await _companyAuth.HasPermissionAsync(userId, id.Value, CompanyPermission.ManageUsers));

            AvailableRoles = await _context.CompanyRoles.OrderBy(r => r.CompanyRoleId).ToListAsync();

            Members = await _context.UserCompanies
                .Include(uc => uc.User)
                .Include(uc => uc.CompanyRole)
                .Where(uc => uc.PartyInfoId == id)
                .ToListAsync();

            PendingInvitations = await _context.CompanyInvitations
                .Include(i => i.CompanyRole)
                .Where(i => i.PartyInfoId == id && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostInviteAsync(int partyInfoId)
        {
            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, partyInfoId, CompanyPermission.ManageUsers);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to invite users to this company.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            var email = InviteEmail?.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                TempData["ErrorMessage"] = "Enter a valid email address.";
                return RedirectToPage(new { id = partyInfoId });
            }

            bool alreadyMember = await _context.UserCompanies
                .Include(uc => uc.User)
                .AnyAsync(uc => uc.PartyInfoId == partyInfoId && uc.User.Email == email);
            if (alreadyMember)
            {
                TempData["ErrorMessage"] = "This person is already a member of this company.";
                return RedirectToPage(new { id = partyInfoId });
            }

            bool alreadyInvited = await _context.CompanyInvitations
                .AnyAsync(i => i.PartyInfoId == partyInfoId && i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow);
            if (alreadyInvited)
            {
                TempData["ErrorMessage"] = "There is already a pending invitation for this email.";
                return RedirectToPage(new { id = partyInfoId });
            }

            var partyInfo = await _context.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == partyInfoId);
            if (partyInfo == null || userId == null) return NotFound();

            var (invitation, rawToken) = await _invitationService.CreateAsync(partyInfoId, email, InviteCompanyRoleId, userId);

            var acceptUrl = Url.Page("/Suppliers/AcceptInvite", pageHandler: null,
                values: new { id = invitation.CompanyInvitationId, token = rawToken }, protocol: Request.Scheme);

            var inviter = await _userManager.FindByIdAsync(userId);
            var roleName = InviteCompanyRoleId.HasValue
                ? (await _context.CompanyRoles.FirstOrDefaultAsync(r => r.CompanyRoleId == InviteCompanyRoleId.Value))?.Name ?? "Member"
                : "Member";

            var placeholders = new Dictionary<string, string>
            {
                { "CompanyName", partyInfo.CompanyName },
                { "InviterName", inviter?.FullName ?? "A team admin" },
                { "RoleName", roleName },
                { "AcceptLink", acceptUrl ?? "#" },
                { "ContactLink", "#" },
                { "Year", DateTime.UtcNow.Year.ToString() },
            };
            var body = await _emailService.LoadEmailTemplate("CompanyInvite/InviteEmailTemplate.html", placeholders);
            await _emailService.SendEmail(email, $"You're invited to join {partyInfo.CompanyName} on eInvWorld", body);

            await _auditService.WriteAsync("Company.UserInvited", new AuditEntry
            {
                Tin = partyInfo.TIN,
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { PartyInfoId = partyInfoId, InvitedEmail = email, InviteCompanyRoleId }),
            });

            TempData["SuccessMessage"] = $"Invitation sent to {email}.";
            return RedirectToPage(new { id = partyInfoId });
        }

        public async Task<IActionResult> OnPostRevokeInviteAsync(int partyInfoId, int invitationId)
        {
            var userId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                bool allowed = userId != null && await _companyAuth.HasPermissionAsync(userId, partyInfoId, CompanyPermission.ManageUsers);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "You do not have permission to manage invitations for this company.";
                    return RedirectToPage(new { id = partyInfoId });
                }
            }

            await _invitationService.RevokeAsync(invitationId, partyInfoId);
            TempData["SuccessMessage"] = "Invitation revoked.";
            return RedirectToPage(new { id = partyInfoId });
        }
    }
}
