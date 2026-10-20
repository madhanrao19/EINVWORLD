using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;

namespace eInvWorld.Pages.Lead
{
    // Was [AllowAnonymous] and binds the whole PartyInfo (IsApproved, links...) with no Turnstile; the public
    // sign-up form is /customer-info/submit (Lead/Submit), so this page is Admin-only.
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public CreateModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["CountryCode"] = new SelectList(_context.CountryCodes, "Code", "Code");
        ViewData["RegTypeCode"] = new SelectList(_context.RegistrationTypes, "Code", "Code");
        ViewData["StateCode"] = new SelectList(_context.StateCodes, "Code", "Code");
            return Page();
        }

        [BindProperty]
        public PartyInfo PartyInfo { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.PartyInfos.Add(PartyInfo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
