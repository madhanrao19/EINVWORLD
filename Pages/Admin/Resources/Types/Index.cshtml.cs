using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EINVWORLD.Data;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Pages.Admin.Resources.Types
{
    public class IndexModel : PageModel
    {
        private readonly EINVWORLD.Data.WebsiteDbContext _context;

        public IndexModel(EINVWORLD.Data.WebsiteDbContext context)
        {
            _context = context;
        }

        public IList<ResourceType> ResourceType { get;set; } = default!;

        public async Task OnGetAsync()
        {
            ResourceType = await _context.ResourceTypes.ToListAsync();
        }
    }
}
