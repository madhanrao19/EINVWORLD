using eInvWorld.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using eInvWorld.Models;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThemeController : ControllerBase
    {
        private readonly GlobalThemeService _themeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ThemeController> _logger;

        public ThemeController(
            GlobalThemeService themeService, 
            UserManager<ApplicationUser> userManager,
            ILogger<ThemeController> logger)
        {
            _themeService = themeService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Get current global theme settings for all users
        /// </summary>
        [HttpGet("global")]
        public async Task<IActionResult> GetGlobalTheme()
        {
            try
            {
                var theme = await _themeService.GetGlobalThemeAsync();
                return Ok(theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting global theme");
                return StatusCode(500, new { message = "Error retrieving theme settings", success = false });
            }
        }

        /// <summary>
        /// Save global theme settings (Admin only)
        /// </summary>
        [HttpPost("save")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveGlobalTheme([FromBody] Dictionary<string, string> themeData)
        {
            try
            {
                _logger.LogInformation("Attempting to save global theme. User: {User}, IsAuthenticated: {IsAuth}", 
                    User?.Identity?.Name, User?.Identity?.IsAuthenticated);

                if (!(User!.Identity?.IsAuthenticated ?? false))
                {
                    _logger.LogWarning("User not authenticated for theme save");
                    return Unauthorized(new { message = "User not authenticated", success = false });
                }

                if (!User.IsInRole("Admin"))
                {
                    _logger.LogWarning("User {User} is not in Admin role", User?.Identity?.Name);
                    return Forbid();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogError("Could not find user from User principal");
                    return Unauthorized(new { message = "User not found", success = false });
                }

                _logger.LogInformation("Saving theme data: {@ThemeData}", themeData);

                var success = await _themeService.SaveThemeFromCustomizerAsync(themeData, user.Id);
                
                if (success)
                {
                    _logger.LogInformation("Global theme updated successfully by admin {AdminUser}", user.UserName);
                    return Ok(new { message = "Theme settings saved successfully", success = true });
                }
                else
                {
                    _logger.LogError("Theme service returned false for user {AdminUser}", user.UserName);
                    return StatusCode(500, new { message = "Failed to save theme settings", success = false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while saving global theme");
                return StatusCode(500, new { message = "Error saving theme settings.", success = false });
            }
        }

        /// <summary>
        /// Test endpoint to check authentication
        /// </summary>
        [HttpGet("test-auth")]
        [Authorize]
        public IActionResult TestAuth()
        {
            return Ok(new 
            { 
                user = User?.Identity?.Name,
                isAuthenticated = User?.Identity?.IsAuthenticated,
                isAdmin = User?.IsInRole("Admin"),
                claims = User?.Claims?.Select(c => new { c.Type, c.Value })
            });
        }
    }
}