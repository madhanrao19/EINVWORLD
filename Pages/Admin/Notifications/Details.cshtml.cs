using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models;

namespace eInvWorld.Pages.Admin.Notifications
{
    public class DetailsModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public DetailsModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public EmailNotification Notification { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var notification = await _context.Notifications.FirstOrDefaultAsync(m => m.Id == id);
            if (notification == null)
            {
                return NotFound();
            }
            else
            {
                Notification = notification;
            }
            return Page();
        }
    }
}
