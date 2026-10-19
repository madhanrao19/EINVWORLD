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

        // Bulk bar: activate / deactivate / delete the ticked email templates.
        public async Task<IActionResult> OnPostBulkAsync(int[] ids, string bulkAction)
        {
            var selected = (ids ?? Array.Empty<int>()).Distinct().Take(500).ToList();
            var templates = await _context.Notifications.Where(n => selected.Contains(n.Id)).ToListAsync();

            switch (bulkAction)
            {
                case "activate":
                case "deactivate":
                    templates.ForEach(n =>
                    {
                        n.IsActive = bulkAction == "activate";
                        n.UpdatedDate = DateTime.Now;
                    });
                    break;
                case "delete":
                    _context.Notifications.RemoveRange(templates);
                    break;
                default:
                    return BadRequest();
            }

            await _context.SaveChangesAsync();
            return RedirectToPage();
        }
    }
}
