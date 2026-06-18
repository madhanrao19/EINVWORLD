using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace eInvWorld.Services
{
    public class RoleSeeder
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context; // ✅ Add DbContext

        public RoleSeeder(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context; // ✅ Inject DbContext
        }

        public async Task SeedRolesAndAdminAsync()
        {
            // ✅ Define Roles
            string[] roleNames = { "Admin", "Supplier", "Buyer", "Director", "Representative" };

            foreach (var roleName in roleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // ✅ Seed Default Users with Assigned Companies
            await SeedUserAsync("admin@einvworld.com", "Admin@123", "Admin", null);
            await SeedUserAsync("supplier@einvworld.com", "Supplier@123", "Supplier", new List<int> { 1, 2 });
            await SeedUserAsync("buyer@einvworld.com", "Buyer@123", "Buyer", new List<int> { 3 });
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
