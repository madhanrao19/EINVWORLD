using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace EINVWORLD.Pages.Admin.System
{
    [Authorize(Roles = "Admin")]
    public class ImageMigrationModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ImageMigrationModel> _logger;

        public ImageMigrationModel(HttpClient httpClient, ILogger<ImageMigrationModel> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool ResourceMigrationCompleted { get; set; } = false;
        public bool LogoMigrationCompleted { get; set; } = false;

        public void OnGet()
        {
            // Check if migrations have been completed by looking for files in external folders
            // This is a simple check - in production you might want to store this in database
        }

        public async Task<IActionResult> OnPostMigrateResourceImagesAsync()
        {
            try
            {
                _logger.LogInformation("Admin '{Admin}' started resource images migration", 
                    User.Identity?.Name ?? "Unknown");

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = await _httpClient.PostAsync($"{baseUrl}/api/resources-migration/migrate-existing-images", null);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MigrationResult>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    StatusMessage = $"✅ Resource images migration completed! Migrated: {result?.Migrated ?? 0} files, Errors: {result?.Errors ?? 0}";
                    ResourceMigrationCompleted = true;

                    _logger.LogInformation("✅ Resource images migration completed by admin '{Admin}'. Migrated: {Migrated}, Errors: {Errors}",
                        User.Identity?.Name ?? "Unknown", result?.Migrated ?? 0, result?.Errors ?? 0);
                }
                else
                {
                    ErrorMessage = $"❌ Resource images migration failed: {response.ReasonPhrase}";
                    _logger.LogError("❌ Resource images migration failed for admin '{Admin}': {Error}",
                        User.Identity?.Name ?? "Unknown", response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Error during resource images migration: {ex.Message}";
                _logger.LogError(ex, "❌ Exception during resource images migration by admin '{Admin}'",
                    User.Identity?.Name ?? "Unknown");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostMigrateCompanyLogosAsync()
        {
            try
            {
                _logger.LogInformation("Admin '{Admin}' started company logos migration", 
                    User.Identity?.Name ?? "Unknown");

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = await _httpClient.PostAsync($"{baseUrl}/api/resources-migration/migrate-existing-company-logos", null);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MigrationResult>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    StatusMessage = $"✅ Company logos migration completed! Migrated: {result?.Migrated ?? 0} files, Errors: {result?.Errors ?? 0}";
                    LogoMigrationCompleted = true;

                    _logger.LogInformation("✅ Company logos migration completed by admin '{Admin}'. Migrated: {Migrated}, Errors: {Errors}",
                        User.Identity?.Name ?? "Unknown", result?.Migrated ?? 0, result?.Errors ?? 0);
                }
                else
                {
                    ErrorMessage = $"❌ Company logos migration failed: {response.ReasonPhrase}";
                    _logger.LogError("❌ Company logos migration failed for admin '{Admin}': {Error}",
                        User.Identity?.Name ?? "Unknown", response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Error during company logos migration: {ex.Message}";
                _logger.LogError(ex, "❌ Exception during company logos migration by admin '{Admin}'",
                    User.Identity?.Name ?? "Unknown");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCleanupOldResourcesAsync()
        {
            try
            {
                _logger.LogInformation("Admin '{Admin}' started cleanup of old resource images", 
                    User.Identity?.Name ?? "Unknown");

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = await _httpClient.PostAsync($"{baseUrl}/api/resources-migration/cleanup-old-images", null);

                if (response.IsSuccessStatusCode)
                {
                    StatusMessage = "✅ Old resource images cleaned up successfully!";
                    _logger.LogInformation("✅ Old resource images cleanup completed by admin '{Admin}'",
                        User.Identity?.Name ?? "Unknown");
                }
                else
                {
                    ErrorMessage = $"❌ Cleanup failed: {response.ReasonPhrase}";
                    _logger.LogError("❌ Old resource images cleanup failed for admin '{Admin}': {Error}",
                        User.Identity?.Name ?? "Unknown", response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Error during cleanup: {ex.Message}";
                _logger.LogError(ex, "❌ Exception during cleanup by admin '{Admin}'",
                    User.Identity?.Name ?? "Unknown");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCleanupOldLogosAsync()
        {
            try
            {
                _logger.LogInformation("Admin '{Admin}' started cleanup of old company logos", 
                    User.Identity?.Name ?? "Unknown");

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var response = await _httpClient.PostAsync($"{baseUrl}/api/resources-migration/cleanup-old-company-logos", null);

                if (response.IsSuccessStatusCode)
                {
                    StatusMessage = "✅ Old company logos cleaned up successfully!";
                    _logger.LogInformation("✅ Old company logos cleanup completed by admin '{Admin}'",
                        User.Identity?.Name ?? "Unknown");
                }
                else
                {
                    ErrorMessage = $"❌ Cleanup failed: {response.ReasonPhrase}";
                    _logger.LogError("❌ Old company logos cleanup failed for admin '{Admin}': {Error}",
                        User.Identity?.Name ?? "Unknown", response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Error during cleanup: {ex.Message}";
                _logger.LogError(ex, "❌ Exception during cleanup by admin '{Admin}'",
                    User.Identity?.Name ?? "Unknown");
            }

            return Page();
        }
    }

    public class MigrationResult
    {
        public string? Message { get; set; }
        public int Migrated { get; set; }
        public int Errors { get; set; }
        public List<string>? Details { get; set; }
        public List<string>? ErrorDetails { get; set; }
    }
}