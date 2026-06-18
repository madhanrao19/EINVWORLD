using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EINVWORLD.Pages
{
    [AllowAnonymous]
    public class TermConditionModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
