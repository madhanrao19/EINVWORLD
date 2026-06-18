using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Pages.Admin.Users
{
    [Authorize(Roles = "Admin")]
    public class ManageUserModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ManageUserModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public IList<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public IDictionary<string, IList<string>> UserRoles { get; set; } = new Dictionary<string, IList<string>>();
        public IList<IdentityRole> AllRoles { get; set; } = new List<IdentityRole>();

        [BindProperty(SupportsGet = true)]
        public string? SearchName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchEmail { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortBy { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        // Pagination Properties
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }

        public async Task OnGetAsync()
        {
            var query = _userManager.Users.AsQueryable();

            // 1. Apply Filtering
            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(u => u.FullName.Contains(SearchName));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(u => u.Email != null && u.Email.Contains(SearchEmail));

            // 2. Apply Sorting
            SortBy = string.IsNullOrEmpty(SortBy) ? "name" : SortBy.ToLower();
            SortOrder = string.IsNullOrEmpty(SortOrder) ? "asc" : SortOrder.ToLower();

            query = SortBy switch
            {
                "email" => SortOrder == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "status" => SortOrder == "asc" ? query.OrderBy(u => u.IsApproved) : query.OrderByDescending(u => u.IsApproved),
                "updatedby" => SortOrder == "asc" ? query.OrderBy(u => u.UpdatedBy) : query.OrderByDescending(u => u.UpdatedBy),
                "updateddate" => SortOrder == "asc" ? query.OrderBy(u => u.UpdatedDate) : query.OrderByDescending(u => u.UpdatedDate),
                _ => SortOrder == "asc" ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
            };

            // 3. Apply Pagination
            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            // Fetch only the current page's records
            Users = await query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 4. Load roles for the fetched users
            foreach (var user in Users)
            {
                UserRoles[user.Id] = await _userManager.GetRolesAsync(user);
            }
            AllRoles = await _roleManager.Roles.ToListAsync();
        }

        public async Task<IActionResult> OnPostAssignRoleAsync(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }

                await _userManager.AddToRoleAsync(user, roleName);

                user.UserType = roleName;
                user.UpdatedBy = User.Identity?.Name ?? "Admin";
                user.UpdatedDate = DateTime.Now;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToPage(new { CurrentPage, SortBy, SortOrder, SearchName, SearchEmail });
        }

        public async Task<IActionResult> OnPostRemoveRolePostAsync(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !string.IsNullOrEmpty(roleName))
            {
                if (await _userManager.IsInRoleAsync(user, roleName))
                {
                    await _userManager.RemoveFromRoleAsync(user, roleName);

                    if (user.UserType == roleName)
                    {
                        user.UserType = "User";
                        user.UpdatedBy = User.Identity?.Name ?? "Admin";
                        user.UpdatedDate = DateTime.Now;
                        await _userManager.UpdateAsync(user);
                    }
                }
            }
            return RedirectToPage(new { CurrentPage, SortBy, SortOrder, SearchName, SearchEmail });
        }

        public async Task<IActionResult> OnGetApproveAsync(string id, int currentPage, string sortBy, string sortOrder, string searchName, string searchEmail)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsApproved = true;
                user.UpdatedBy = User.Identity?.Name ?? "Admin";
                user.UpdatedDate = DateTime.Now;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToPage(new { CurrentPage = currentPage, SortBy = sortBy, SortOrder = sortOrder, SearchName = searchName, SearchEmail = searchEmail });
        }

        public async Task<IActionResult> OnGetDisapproveAsync(string id, int currentPage, string sortBy, string sortOrder, string searchName, string searchEmail)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsApproved = false;
                user.UpdatedBy = User.Identity?.Name ?? "Admin";
                user.UpdatedDate = DateTime.Now;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToPage(new { CurrentPage = currentPage, SortBy = sortBy, SortOrder = sortOrder, SearchName = searchName, SearchEmail = searchEmail });
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, int currentPage, string sortBy, string sortOrder, string searchName, string searchEmail)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null) await _userManager.DeleteAsync(user);
            return RedirectToPage(new { CurrentPage = currentPage, SortBy = sortBy, SortOrder = sortOrder, SearchName = searchName, SearchEmail = searchEmail });
        }
    }
}