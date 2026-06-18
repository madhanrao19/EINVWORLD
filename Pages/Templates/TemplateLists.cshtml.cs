using eInvWorld.Data;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Templates;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Templates
{
    [Authorize(Roles = "Admin,Supplier")]
    public class TemplateListsModel : SupplierBasePage
    {
        private new readonly eInvWorld.Data.ApplicationDbContext _context;

        public TemplateListsModel(eInvWorld.Data.ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public IList<InvoiceTemplate> InvoiceTemplate { get;set; } = default!;

        [BindProperty]
        public int TemplateId { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public string CreatedByUserId { get; set; } = null!;



        public async Task<IActionResult> OnPostToggleFavoriteAsync(int templateId)
        {
            var template = await _context.InvoiceTemplates.FindAsync(templateId);
            if (template == null)
            {
                return NotFound();
            }

            template.IsFavorite = !template.IsFavorite;
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }


        public async Task OnGetAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            InvoiceTemplate = await _context.InvoiceTemplates
               .Where(t => t.CreatedByUserId == userId) // 🔒 Only fetch templates created by this user
               .Include(t => t.Supplier)
               .Include(t => t.Customer)
               .Include(t => t.PublicCustomer)
               .OrderByDescending(t => t.IsFavorite)
               .ThenBy(t => t.TemplateName)
               .ToListAsync();
        }

        [BindProperty]
        public List<int> SelectedTemplateIds { get; set; } = new();

        [BindProperty]
        public int DeleteTemplateId { get; set; }
        //public async Task<IActionResult> OnPostDeleteSingleAsync()
        //{
        //    var template = await _context.InvoiceTemplates.FindAsync(DeleteTemplateId);
        //    if (template != null)
        //    {
        //        _context.InvoiceTemplates.Remove(template);
        //        await _context.SaveChangesAsync();
        //        StatusMessage = "Template deleted successfully.";

        //    }

        //    return RedirectToPage();
        //}
        public async Task<IActionResult> OnPostDeleteSingleAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var template = await _context.InvoiceTemplates
                .Where(t => t.Id == DeleteTemplateId && t.CreatedByUserId == userId)
                .FirstOrDefaultAsync();

            if (template == null)
            {
                return Unauthorized(); // or return NotFound() to mask existence
            }

            _context.InvoiceTemplates.Remove(template);
            await _context.SaveChangesAsync();

            StatusMessage = "Template deleted successfully.";
            return RedirectToPage();
        }


        [BindProperty]
        public string SelectedTemplateIdsRaw { get; set; } = string.Empty;



        //public async Task<IActionResult> OnPostDeleteMultipleAsync()
        //{
        //    var idList = SelectedTemplateIdsRaw
        //        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        //        .Select(id => int.TryParse(id, out var parsedId) ? parsedId : (int?)null)
        //        .Where(id => id.HasValue)
        //        .Select(id => id.Value)
        //        .ToList();

        //    if (!idList.Any()) return RedirectToPage();

        //    var templates = await _context.InvoiceTemplates
        //        .Where(t => idList.Contains(t.Id))
        //        .ToListAsync();

        //    if (templates.Any())
        //    {
        //        _context.InvoiceTemplates.RemoveRange(templates);
        //        await _context.SaveChangesAsync();
        //        StatusMessage = $"{templates.Count} template(s) deleted successfully.";
        //    }

        //    return RedirectToPage();
        //}

        public async Task<IActionResult> OnPostDeleteMultipleAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name ?? "Unknown";

            var idList = SelectedTemplateIdsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var parsedId) ? parsedId : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (!idList.Any()) return RedirectToPage();

            // 🔒 Admin override check (optional)
            bool isAdmin = User.IsInRole("Admin");

            var templates = await _context.InvoiceTemplates
                .Where(t => idList.Contains(t.Id) && (t.CreatedByUserId == userId || isAdmin))
                .ToListAsync();

            if (templates.Any())
            {
                // ✅ Log deletions
                foreach (var template in templates)
                {
                    _context.InvoiceHistories.Add(new InvoiceHistory
                    {
                        InvoiceNo = template.TemplateName ?? "",
                        Action = "DeletedTemplate",
                        Timestamp = GetMYTime(),
                        PerformedBy = userName,
                        Remarks = $"Deleted template ID: {template.Id}"
                    });
                }

                _context.InvoiceTemplates.RemoveRange(templates);
                await _context.SaveChangesAsync();

                StatusMessage = $"{templates.Count} template(s) deleted successfully.";
            }
            else
            {
                StatusMessage = "No templates deleted. You can only delete your own templates.";
            }

            return RedirectToPage();
        }

        private DateTime GetMYTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
        }



    }
}
