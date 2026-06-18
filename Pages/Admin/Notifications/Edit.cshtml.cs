using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Pages.Admin.Notifications
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public EmailNotification Notification { get; set; } = default!;

        public SelectList NotificationTypeOptions { get; set; } = null!;

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

            Notification = notification;

            // Populate NotificationTypeOptions for the dropdown
            NotificationTypeOptions = new SelectList(
                Enum.GetValues(typeof(NotificationType))
                    .Cast<NotificationType>()
                    .Select(e => new
                    {
                        Value = e,
                        Text = e.GetType()
                                 .GetField(e.ToString())!
                                 .GetCustomAttributes(typeof(DisplayAttribute), false)
                                 .Cast<DisplayAttribute>()
                                 .FirstOrDefault()?.Name ?? e.ToString()
                    }),
                "Value", "Text");

            return Page(); // Return the page with the populated model
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Repopulate NotificationTypeOptions if the model state is invalid
                NotificationTypeOptions = new SelectList(
                    Enum.GetValues(typeof(NotificationType))
                        .Cast<NotificationType>()
                        .Select(e => new
                        {
                            Value = e,
                            Text = e.GetType()
                                     .GetField(e.ToString())
                                     ?.GetCustomAttributes(typeof(DisplayAttribute), false)
                                     .Cast<DisplayAttribute>()
                                     .FirstOrDefault()?.Name ?? e.ToString()
                        }),
                    "Value", "Text");
                return Page();
            }

            // Set the updated date to now
            Notification.UpdatedDate = DateTime.Now;
            _context.Attach(Notification).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NotificationExists(Notification.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool NotificationExists(int id)
        {
            return _context.Notifications.Any(e => e.Id == id);
        }
    }
}
