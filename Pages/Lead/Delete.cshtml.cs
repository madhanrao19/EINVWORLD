using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;

namespace eInvWorld.Pages.Lead
{
    public class DeleteModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public DeleteModel(eInvWorld.Data.ApplicationDbContext context)
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

            var partyinfo = await _context.PartyInfos.FirstOrDefaultAsync(m => m.PartyInfoId == id);

            if (partyinfo == null)
            {
                return NotFound();
            }
            else
            {
                PartyInfo = partyinfo;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var partyinfo = await _context.PartyInfos.FindAsync(id);
            if (partyinfo != null)
            {
                PartyInfo = partyinfo;
                _context.PartyInfos.Remove(PartyInfo);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./List");
        }
    }
}
