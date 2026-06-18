// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eInvWorld.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LockoutModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LockoutModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public TimeSpan? RemainingLockoutTime { get; private set; }

        public async Task OnGetAsync()
        {
            // Try to extract username from claims if available
            var userName = User?.Identity?.Name;

            if (!string.IsNullOrWhiteSpace(userName))
            {
                var user = await _userManager.FindByNameAsync(userName);

                if (user?.LockoutEnd.HasValue == true && user.LockoutEnd > DateTimeOffset.UtcNow)
                {
                    RemainingLockoutTime = user.LockoutEnd.Value - DateTimeOffset.UtcNow;
                }
            }
        }
    }
}
