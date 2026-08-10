using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace eInvWorld.Services
{
    public class RoleSeeder
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context; // ✅ Add DbContext
        private readonly IConfiguration _config;
        private readonly ILogger<RoleSeeder> _logger;

        public RoleSeeder(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IConfiguration config, ILogger<RoleSeeder> logger)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context; // ✅ Inject DbContext
            _config = config;
            _logger = logger;
        }

        public async Task SeedRolesAndAdminAsync()
        {
            // ✅ Define Roles (always — harmless, and the app's authorization depends on them existing)
            string[] roleNames = { "Admin", "Supplier", "Buyer", "Director", "Representative" };

            foreach (var roleName in roleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Default demo users (admin@/supplier@/buyer@einvworld.com) are seeded ONLY when explicitly
            // enabled. They ship guessable passwords, so this MUST be off in Production (set
            // Seeding:SeedDefaultUsers=false there). Existing installs are unaffected — SeedUserAsync only
            // creates a user when that email doesn't already exist.
            if (!_config.GetValue<bool>("Seeding:SeedDefaultUsers", true))
            {
                _logger.LogInformation("Skipping default-user seeding (Seeding:SeedDefaultUsers=false).");
                return;
            }

            // Passwords are overridable via config/env (Seeding:DefaultAdminPassword, etc.) so a non-prod
            // environment that DOES seed can still avoid the well-known defaults.
            string adminPwd    = _config["Seeding:DefaultAdminPassword"]    ?? "Admin@123";
            string supplierPwd = _config["Seeding:DefaultSupplierPassword"] ?? "Supplier@123";
            string buyerPwd    = _config["Seeding:DefaultBuyerPassword"]    ?? "Buyer@123";

            // ✅ Seed Default Users with Assigned Companies. Admins are still forced to enrol 2FA on first
            // login by AdminMfaEnforcementMiddleware, so a seeded admin can't be used without MFA.
            await SeedUserAsync("admin@einvworld.com", adminPwd, "Admin", null);
            await SeedUserAsync("supplier@einvworld.com", supplierPwd, "Supplier", new List<int> { 1, 2 });
            // PartyInfoId 3 (the LHDN generic "Foreign Buyer / Shipping Recipient" placeholder TIN
            // EI00000000020) can never complete LHDN's intermediary OAuth token exchange - it's not a
            // real onboarded company, so login succeeds past the TIN check and then fails at token
            // retrieval ("LHDN rejected intermediary token... unauthorized_client"). Use a real onboarded
            // company instead (PartyInfoId 12, "Datamation (M) Sdn. Bhd.", TIN C2899917070 - operator
            // confirmed 2026-08-10) so the demo Buyer account can actually reach a working dashboard.
            await SeedUserAsync("buyer@einvworld.com", buyerPwd, "Buyer", new List<int> { 12 });
        }

        private async Task SeedUserAsync(string email, string password, string role, List<int>? companyIds)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    IsApproved = true,
                    IsDefaultUser = true,
                    IsActive = true,
                    UserType = role
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded) return;

                await _userManager.AddToRoleAsync(user, role);
            }

            // ✅ Ensure company links exist, on every startup — not just when the user is first created.
            // A fresh database has no PartyInfos yet, so a link can legitimately be skipped the first time
            // (otherwise the FK FK_UserCompanies_PartyInfos_PartyInfoId would crash startup seeding). Once
            // that PartyInfo exists (e.g. after company seed data lands on a later deploy), this backfills
            // the missing link instead of leaving the demo account permanently unable to log in — without
            // this, "FindByEmailAsync(email) == null" being false on every later startup meant the link was
            // never retried, which is exactly what happened to supplier@einvworld.com/buyer@einvworld.com
            // on Staging (flagged in CHANGELOG 2026-08-01, still broken as of this pass).
            if (companyIds != null)
            {
                var existingLinks = await _context.UserCompanies
                    .Where(uc => uc.UserId == user.Id)
                    .Select(uc => uc.PartyInfoId)
                    .ToListAsync();

                var changed = false;
                foreach (var companyId in companyIds)
                {
                    if (existingLinks.Contains(companyId)) continue;

                    if (await _context.PartyInfos.AnyAsync(p => p.PartyInfoId == companyId))
                    {
                        _context.UserCompanies.Add(new UserCompany
                        {
                            UserId = user.Id,
                            PartyInfoId = companyId
                        });
                        changed = true;
                    }
                    else
                    {
                        _logger.LogWarning("Seeding {Email}: skipping company link {CompanyId} — no such PartyInfo yet.", email, companyId);
                    }
                }

                if (changed) await _context.SaveChangesAsync();
            }
        }
    }
}
