using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using eInvWorld.Data;
using eInvWorld.Models;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Pages.Admin.Notifications
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
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

            Notification = new EmailNotification(); // Initialize the Notification

            return Page();
        }

        [BindProperty]
        public EmailNotification Notification { get; set; } = default!; // Using default! to suppress warnings

        public SelectList NotificationTypeOptions { get; set; } = null!;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page(); // Return the page if model state is invalid
            }

            // Retrieve the user's name from the context (User.Identity.Name is a common approach)
            var userName = User.Identity?.Name;

            // Replace the placeholder in the notification body with the user's name
            Notification.Body = Notification.Body.Replace("{{UserName}}", userName);

            _context.Notifications.Add(Notification); // Add the new notification to the context
            await _context.SaveChangesAsync(); // Save changes to the database

            return RedirectToPage("./Index"); // Redirect to Index page after successful creation
        }
    }
}
