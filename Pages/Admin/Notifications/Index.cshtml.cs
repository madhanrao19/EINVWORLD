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
    public class IndexModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public IndexModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<EmailNotification> Notification { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Notification = await _context.Notifications.ToListAsync();
        }
    }
}
