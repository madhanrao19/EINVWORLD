using eInvWorld.Data;
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

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TablePreferencesController> _logger;

        public TablePreferencesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            ILogger<TablePreferencesController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>Saved choices for the tables on the current page: <c>GET api/table-preferences?keys=items&amp;keys=leads</c>.</summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string[] keys, CancellationToken ct)
        {
            if (keys.Length == 0 || keys.Length > MaxKeysPerRequest) return BadRequest();
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            return Ok(await TablePreferences.LoadAsync(_db, userId, keys, ct));
        }

        /// <summary>Saves one table's choices; empty lists remove the entry ("Reset to default").</summary>
        [HttpPut("{key}")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(16 * 1024)] // 60 names x 120 chars fits easily; don't parse megabytes first
        public async Task<IActionResult> Put(string key, [FromBody] TableColumnPreference body, CancellationToken ct)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var pref = TablePreferences.IsValidKey(key) ? TablePreferences.Normalize(body) : null;
            var result = pref == null
                ? TablePreferenceSaveResult.Rejected
                : await TablePreferences.SaveAsync(_db, userId, key, pref, ct);

            switch (result)
            {
                case TablePreferenceSaveResult.Saved:
                case TablePreferenceSaveResult.Unchanged:
                    return NoContent();
                case TablePreferenceSaveResult.Conflict:
                    // Another save changed the preferences first; the client keeps its layout and saves again on the next change.
                    _logger.LogInformation("Table preference save conflicted with a concurrent save; client will retry");
                    return Conflict();
                default:
                    return BadRequest(); // invalid key/body, or the count/size caps
            }
        }
    }
}
