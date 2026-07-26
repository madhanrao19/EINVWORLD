using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using EINVWORLD.Services.Audit;
using EINVWORLD.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Suppliers
{
    /// <summary>
    /// Redeems a company invitation. Anonymous by design (the invitee usually has no account yet) —
    /// every write is gated by a validated, single-use, hashed token, never by the caller's identity.
    /// The invitee ALWAYS sets their own password here; nothing here ever accepts one from an admin.
    /// </summary>
    [AllowAnonymous]
    public class AcceptInviteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ICompanyInvitationService _invitationService;
        private readonly IAuditService _auditService;

        public AcceptInviteModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, ICompanyInvitationService invitationService, IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _invitationService = invitationService;
            _auditService = auditService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string CompanyName { get; private set; } = string.Empty;
        public string RoleName { get; private set; } = "Member";
        public string InviteeEmail { get; private set; } = string.Empty;
        public bool AccountAlreadyExists { get; private set; }

        [BindProperty]
        public RegisterInput Input { get; set; } = new();

        public class RegisterInput
        {
            [Required, StringLength(200)]
            public string FullName { get; set; } = string.Empty;

            [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var result = await _invitationService.ValidateAsync(Id, Token);
            IsValid = result.IsValid;
            ErrorMessage = result.Error;

            if (result.Invitation != null)
            {
                CompanyName = result.Invitation.PartyInfo?.CompanyName ?? "this company";
                RoleName = result.Invitation.CompanyRole?.Name ?? "Member";
                InviteeEmail = result.Invitation.Email;
                AccountAlreadyExists = await _userManager.FindByEmailAsync(InviteeEmail) != null;
            }

            return Page();
        }

        /// <summary>New-account path — only reached when no ApplicationUser exists yet for the invited email.</summary>
        public async Task<IActionResult> OnPostRegisterAsync()
        {
            var result = await _invitationService.ValidateAsync(Id, Token);
            if (!result.IsValid || result.Invitation == null)
            {
                ErrorMessage = result.Error;
                return Page();
            }

            var invitation = result.Invitation;
            CompanyName = invitation.PartyInfo?.CompanyName ?? "this company";
            RoleName = invitation.CompanyRole?.Name ?? "Member";
            InviteeEmail = invitation.Email;
            IsValid = true;

            if (await _userManager.FindByEmailAsync(invitation.Email) != null)
            {
                AccountAlreadyExists = true;
                return Page();
            }

            if (!ModelState.IsValid)
                return Page();

            var newUser = new ApplicationUser
            {
                UserName = invitation.Email,
                Email = invitation.Email,
                FullName = Input.FullName,
                EmailConfirmed = true, // proven by possession of the single-use invite token mailed to this address
                IsApproved = true,
                UserType = "Supplier",
                UpdatedBy = "Company Invite",
                UpdatedDate = DateTime.Now,
            };

            var createResult = await _userManager.CreateAsync(newUser, Input.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            await _userManager.AddToRoleAsync(newUser, "Supplier");

            bool hasPrimaryAlready = await _context.UserCompanies.AnyAsync(uc => uc.UserId == newUser.Id && uc.IsPrimaryCompany);
            _context.UserCompanies.Add(new Models.InputModel.UserCompany
            {
                UserId = newUser.Id,
                PartyInfoId = invitation.PartyInfoId,
                CompanyRoleId = invitation.CompanyRoleId,
                HasCompanyAccess = true,
                IsViewOnly = false,
                IsPrimaryCompany = !hasPrimaryAlready,
            });
            await _context.SaveChangesAsync();

            await _invitationService.MarkAcceptedAsync(invitation.CompanyInvitationId);
            await _auditService.WriteAsync("Company.InviteAccepted", new AuditEntry
            {
                Tin = invitation.PartyInfo?.TIN,
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { invitation.PartyInfoId, AcceptedByNewAccount = true, Email = invitation.Email }),
            });

            await _signInManager.SignInAsync(newUser, isPersistent: false);
            TempData["SuccessMessage"] = $"Welcome! You've joined {CompanyName}.";
            return RedirectToPage("/Dashboard/Dashboard");
        }

        /// <summary>Existing-account path — the visitor must already be signed in as the invited email.</summary>
        public async Task<IActionResult> OnPostAcceptExistingAsync()
        {
            var result = await _invitationService.ValidateAsync(Id, Token);
            if (!result.IsValid || result.Invitation == null)
            {
                ErrorMessage = result.Error;
                return Page();
            }

            var invitation = result.Invitation;
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { ReturnUrl = Request.Path + Request.QueryString });
            }

            if (!string.Equals(currentUser.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"This invitation was sent to {invitation.Email}. Please log out and sign in with that account to accept it.";
                CompanyName = invitation.PartyInfo?.CompanyName ?? "this company";
                RoleName = invitation.CompanyRole?.Name ?? "Member";
                InviteeEmail = invitation.Email;
                IsValid = true;
                AccountAlreadyExists = true;
                return Page();
            }

            bool alreadyMember = await _context.UserCompanies.AnyAsync(uc => uc.UserId == currentUser.Id && uc.PartyInfoId == invitation.PartyInfoId);
            if (!alreadyMember)
            {
                bool hasPrimaryAlready = await _context.UserCompanies.AnyAsync(uc => uc.UserId == currentUser.Id && uc.IsPrimaryCompany);
                _context.UserCompanies.Add(new Models.InputModel.UserCompany
                {
                    UserId = currentUser.Id,
                    PartyInfoId = invitation.PartyInfoId,
                    CompanyRoleId = invitation.CompanyRoleId,
                    HasCompanyAccess = true,
                    IsViewOnly = false,
                    IsPrimaryCompany = !hasPrimaryAlready,
                });
                await _context.SaveChangesAsync();
            }

            await _invitationService.MarkAcceptedAsync(invitation.CompanyInvitationId);
            await _auditService.WriteAsync("Company.InviteAccepted", new AuditEntry
            {
                Tin = invitation.PartyInfo?.TIN,
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { invitation.PartyInfoId, AcceptedByNewAccount = false, Email = invitation.Email }),
            });

            TempData["SuccessMessage"] = $"You've joined {invitation.PartyInfo?.CompanyName}.";
            return RedirectToPage("/Dashboard/Dashboard");
        }
    }
}
