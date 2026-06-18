using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EINVWORLD.Pages.Error
{
    public class InternalModel : PageModel
    {
        public void OnGet()
        {
            Response.StatusCode = 500;

        }
    }
}
