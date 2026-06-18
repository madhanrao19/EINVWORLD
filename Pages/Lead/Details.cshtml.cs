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
    public class DetailsModel : PageModel
    {
        private readonly eInvWorld.Data.ApplicationDbContext _context;

        public DetailsModel(eInvWorld.Data.ApplicationDbContext context)
        {
            _context = context;
        }

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
    }
}
