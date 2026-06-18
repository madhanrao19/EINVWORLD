using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Lead
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public EditModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PartyInfo PartyInfo { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var partyinfo =  await _context.PartyInfos.FirstOrDefaultAsync(m => m.PartyInfoId == id);
            if (partyinfo == null)
            {
                return NotFound();
            }
            PartyInfo = partyinfo;
           ViewData["CountryCode"] = new SelectList(_context.CountryCodes, "Code", "Code");
           ViewData["RegTypeCode"] = new SelectList(_context.RegistrationTypes, "Code", "Code");
           ViewData["StateCode"] = new SelectList(_context.StateCodes, "Code", "Code");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(PartyInfo).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PartyInfoExists(PartyInfo.PartyInfoId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./List");
        }

        private bool PartyInfoExists(int id)
        {
            return _context.PartyInfos.Any(e => e.PartyInfoId == id);
        }
    }
}
