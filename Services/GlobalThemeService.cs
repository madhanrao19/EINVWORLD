using eInvWorld.Data;
using eInvWorld.Models.Settings;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Services
{
    public class GlobalThemeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GlobalThemeService> _logger;

        public GlobalThemeService(ApplicationDbContext context, ILogger<GlobalThemeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get the global theme settings. Creates default if none exists.
        /// </summary>
        public async Task<GlobalThemeSettings> GetGlobalThemeAsync()
        {
            var theme = await _context.GlobalThemeSettings.OrderByDescending(x => x.Id).FirstOrDefaultAsync();

            if (theme == null)
            {
                // Create default theme settings if none exist
                theme = new GlobalThemeSettings
                {
                    DataLayout = "vertical",
                    DataTheme = "default", 
                    DataThemeColors = "green", // eInvWorld brand color
                    DataTopbar = "light",
                    DataSidebar = "dark",
                    DataSidebarSize = "lg",
                    DataSidebarImage = "none",
                    DataLayoutWidth = "fluid",
                    DataLayoutPosition = "fixed",
                    DataLayoutStyle = "default",
                    DataBsTheme = "light",
                    DataPreloader = "disable",
                    DataBodyImage = "none",
                    DataSidebarVisibility = "show",
                    LastUpdated = DateTime.UtcNow
                };

                _context.GlobalThemeSettings.Add(theme);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created default global theme settings");
            }

            return theme;
        }

        /// <summary>
        /// Update global theme settings (Admin only)
        /// </summary>
        public async Task<bool> UpdateGlobalThemeAsync(GlobalThemeSettings themeSettings, string adminUserId)
        {
            try
            {
                var existingTheme = await _context.GlobalThemeSettings.FirstOrDefaultAsync();
                
                if (existingTheme == null)
                {
                    // Create new if doesn't exist
                    themeSettings.UpdatedBy = adminUserId;
                    themeSettings.LastUpdated = DateTime.UtcNow;
                    _context.GlobalThemeSettings.Add(themeSettings);
                }
                else
                {
                    // Update existing
                    existingTheme.DataLayout = themeSettings.DataLayout;
                    existingTheme.DataTheme = themeSettings.DataTheme;
                    existingTheme.DataThemeColors = themeSettings.DataThemeColors;
                    existingTheme.DataTopbar = themeSettings.DataTopbar;
                    existingTheme.DataSidebar = themeSettings.DataSidebar;
                    existingTheme.DataSidebarSize = themeSettings.DataSidebarSize;
                    existingTheme.DataSidebarImage = themeSettings.DataSidebarImage;
                    existingTheme.DataLayoutWidth = themeSettings.DataLayoutWidth;
                    existingTheme.DataLayoutPosition = themeSettings.DataLayoutPosition;
                    existingTheme.DataLayoutStyle = themeSettings.DataLayoutStyle;
                    existingTheme.DataBsTheme = themeSettings.DataBsTheme;
                    existingTheme.DataPreloader = themeSettings.DataPreloader;
                    existingTheme.DataBodyImage = themeSettings.DataBodyImage;
                    existingTheme.DataSidebarVisibility = themeSettings.DataSidebarVisibility;
                    existingTheme.UpdatedBy = adminUserId;
                    existingTheme.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Global theme settings updated by admin user {AdminUserId}", adminUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global theme settings");
                return false;
            }
        }

        /// <summary>
        /// Save theme settings from admin customizer
        /// </summary>
        public async Task<bool> SaveThemeFromCustomizerAsync(Dictionary<string, string> themeData, string adminUserId)
        {
            try
            {
                _logger.LogInformation("SaveThemeFromCustomizerAsync called with adminUserId: {AdminUserId}", adminUserId);
                _logger.LogInformation("Received theme data: {@ThemeData}", themeData);

                if (themeData == null || !themeData.Any())
                {
                    _logger.LogWarning("No theme data provided");
                    return false;
                }

                var theme = await GetGlobalThemeAsync();
                _logger.LogInformation("Retrieved existing theme: {@ExistingTheme}", theme);
                
                // Map theme data from customizer
                if (themeData.ContainsKey("data-layout")) 
                {
                    theme.DataLayout = themeData["data-layout"];
                    _logger.LogDebug("Updated DataLayout to: {Value}", theme.DataLayout);
                }
                if (themeData.ContainsKey("data-theme")) 
                {
                    theme.DataTheme = themeData["data-theme"];
                    _logger.LogDebug("Updated DataTheme to: {Value}", theme.DataTheme);
                }
                if (themeData.ContainsKey("data-theme-colors")) 
                {
                    theme.DataThemeColors = themeData["data-theme-colors"];
                    _logger.LogDebug("Updated DataThemeColors to: {Value}", theme.DataThemeColors);
                }
                if (themeData.ContainsKey("data-topbar")) theme.DataTopbar = themeData["data-topbar"];
                if (themeData.ContainsKey("data-sidebar")) theme.DataSidebar = themeData["data-sidebar"];
                if (themeData.ContainsKey("data-sidebar-size")) theme.DataSidebarSize = themeData["data-sidebar-size"];
                if (themeData.ContainsKey("data-sidebar-image")) theme.DataSidebarImage = themeData["data-sidebar-image"];
                if (themeData.ContainsKey("data-layout-width")) theme.DataLayoutWidth = themeData["data-layout-width"];
                if (themeData.ContainsKey("data-layout-position")) theme.DataLayoutPosition = themeData["data-layout-position"];
                if (themeData.ContainsKey("data-layout-style")) theme.DataLayoutStyle = themeData["data-layout-style"];
                if (themeData.ContainsKey("data-bs-theme")) theme.DataBsTheme = themeData["data-bs-theme"];
                if (themeData.ContainsKey("data-preloader")) theme.DataPreloader = themeData["data-preloader"];
                if (themeData.ContainsKey("data-body-image")) theme.DataBodyImage = themeData["data-body-image"];
                if (themeData.ContainsKey("data-sidebar-visibility")) theme.DataSidebarVisibility = themeData["data-sidebar-visibility"];

                _logger.LogInformation("Calling UpdateGlobalThemeAsync with updated theme: {@UpdatedTheme}", theme);
                var result = await UpdateGlobalThemeAsync(theme, adminUserId);
                _logger.LogInformation("UpdateGlobalThemeAsync returned: {Result}", result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving theme from customizer for admin {AdminUserId}", adminUserId);
                return false;
            }
        }
    }
}