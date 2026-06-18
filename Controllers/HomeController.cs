using eInvWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Services;

namespace eInvWorld.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITokenService _tokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ITokenService tokenService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _tokenService = tokenService;
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("Home/LoginAsTaxpayer")]
        public async Task<IActionResult> LoginAsTaxpayer()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized("User not logged in.");

            var userTin = await _context.UserCompanies
                .Where(uc => uc.UserId == user.Id)
                .Include(uc => uc.PartyInfo)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userTin)) return BadRequest("User's TIN is missing.");

            // ✅ Store the TIN in session so TokenService can use it
            HttpContext.Session.SetString("UserTIN", userTin);

            // ✅ Get token using centralized logic
            var accessToken = await _tokenService.GetAccessTokenForTIN(userTin);
            HttpContext.Session.SetString("AccessToken", accessToken);

            return RedirectToAction("Index");
        }

        [HttpPost("Home/LoginAsIntermediary")]
        public async Task<IActionResult> LoginAsIntermediary()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized("User not logged in.");

            var userTin = await _context.UserCompanies
                .Where(uc => uc.UserId == user.Id)
                .Include(uc => uc.PartyInfo)
                .Select(uc => uc.PartyInfo.TIN)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userTin)) return BadRequest("User's TIN is missing.");

            // ✅ Store the TIN in session for intermediary access
            HttpContext.Session.SetString("UserTIN", userTin);

            // ✅ Get token using centralized logic
            var accessToken = await _tokenService.GetAccessTokenForTIN(userTin);
            HttpContext.Session.SetString("AccessToken", accessToken);

            return RedirectToAction("Index");
        }
    }
}
