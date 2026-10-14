using eInvWorld.Helpers;
using eInvWorld.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace eInvWorld.Controllers
{
    /// <summary>
    /// The signed-in user's column choices for list tables (einvworld-table.js "Columns" menu), saved on
    /// their own account so they follow them to any browser. There is no user id in any route: every
    /// call reads or writes only the caller's own record.
    /// </summary>
    [Route("api/table-preferences")]
    [ApiController]
    [Authorize]
    public class TablePreferencesController : ControllerBase
    {
        private const int MaxKeysPerRequest = 20;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TablePreferencesController> _logger;

        public TablePreferencesController(UserManager<ApplicationUser> userManager, ILogger<TablePreferencesController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>Saved choices for the tables on the current page: <c>GET api/table-preferences?keys=items&amp;keys=leads</c>.</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string[] keys)
        {
            if (keys.Length == 0 || keys.Length > MaxKeysPerRequest) return BadRequest();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(TablePreferences.Read(user.UserPreferences, keys));
        }

        /// <summary>Saves one table's choices; empty lists remove the entry ("Reset to default").</summary>
        [HttpPut("{key}")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(16 * 1024)] // 60 names x 120 chars fits easily; don't parse megabytes first
        public async Task<IActionResult> Put(string key, [FromBody] TableColumnPreference body)
        {
            var pref = TablePreferences.Normalize(body);
            if (!TablePreferences.IsValidKey(key) || pref == null) return BadRequest();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var json = TablePreferences.Write(user.UserPreferences, key, pref);
            if (json == null) return BadRequest(new { message = "Too many saved table layouts." }); // count or size cap
            if (json == user.UserPreferences) return NoContent();

            user.UserPreferences = json;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded) return NoContent();

            // A concurrent save of the same user record: the client keeps its layout and retries on the next change.
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))) return Conflict();
            _logger.LogWarning("Saving table preferences failed for table {TableKey}: {Errors}",
                key, string.Join(", ", result.Errors.Select(e => e.Code)));
            return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError);
        }
    }
}
