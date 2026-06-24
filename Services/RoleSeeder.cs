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
            await SeedUserAsync("buyer@einvworld.com", buyerPwd, "Buyer", new List<int> { 3 });
        }

        private async Task SeedUserAsync(string email, string password, string role, List<int>? companyIds)
        {
            if (await _userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser
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

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, role);

                    // ✅ Assign user to multiple companies
                    if (companyIds != null)
                    {
                        foreach (var companyId in companyIds)
                        {
                            _context.UserCompanies.Add(new UserCompany
                            {
                                UserId = user.Id,
                                PartyInfoId = companyId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
