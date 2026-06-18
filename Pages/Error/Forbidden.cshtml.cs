using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EINVWORLD.Pages.Error
{
    public class ForbiddenModel : PageModel
    {
        public void OnGet()
        {
            Response.StatusCode = 403;

        }
    }
}
