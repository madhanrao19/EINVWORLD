using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Models;
using EINVWORLD.Data;
using eInvWorld.Data;

namespace EINVWORLD.Controllers
{
    [Route("api/resources-migration")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ResourcesMigrationController : ControllerBase
    {
        private readonly FilePathConfig _filePathConfig;
        private readonly IWebHostEnvironment _env;
        private readonly WebsiteDbContext _websiteContext;
        private readonly ApplicationDbContext _appContext;
        private readonly ILogger<ResourcesMigrationController> _logger;

        public ResourcesMigrationController(
            IOptions<FilePathConfig> filePathConfig,
            IWebHostEnvironment env,
            WebsiteDbContext websiteContext,
            ApplicationDbContext appContext,
            ILogger<ResourcesMigrationController> logger)
        {
            _filePathConfig = filePathConfig.Value;
            _env = env;
            _websiteContext = websiteContext;
            _appContext = appContext;
            _logger = logger;
        }

        [HttpPost("migrate-existing-images")]
        public async Task<IActionResult> MigrateExistingImages()
        {
            try
            {
                var migrationResults = new List<string>();
                var errorResults = new List<string>();

                // Source path: wwwroot/images/resources
                var sourceBasePath = Path.Combine(_env.WebRootPath, "images", "resources");
                
                // Destination path: External configured path
                var destBasePath = _filePathConfig.ResourceImagesFolder;

                // Ensure destination base directory exists
                Directory.CreateDirectory(destBasePath);

                if (!Directory.Exists(sourceBasePath))
                {
                    return Ok(new { 
                        message = "No existing resources folder found to migrate",
                        migrated = 0,
                        errors = 0
                    });
                }

                // Get all category folders (article, announcement, general, etc.)
                var categoryFolders = Directory.GetDirectories(sourceBasePath);

                foreach (var categoryFolder in categoryFolders)
                {
                    var categoryName = Path.GetFileName(categoryFolder);
                    _logger.LogInformation($"Processing category: {categoryName}");

                    // Create destination category folder
                    var destCategoryPath = Path.Combine(destBasePath, categoryName);
                    Directory.CreateDirectory(destCategoryPath);

                    // Process full and thumb folders
                    var sizeFolders = Directory.GetDirectories(categoryFolder);
                    foreach (var sizeFolder in sizeFolders)
                    {
                        var sizeName = Path.GetFileName(sizeFolder); // "full" or "thumb"
                        
                        // Create destination size folder
                        var destSizePath = Path.Combine(destCategoryPath, sizeName);
                        Directory.CreateDirectory(destSizePath);

                        // Move all files in this folder
                        var files = Directory.GetFiles(sizeFolder);
                        foreach (var file in files)
                        {
                            try
                            {
                                var fileName = Path.GetFileName(file);
                                var destFile = Path.Combine(destSizePath, fileName);

                                // Copy file to external location
                                System.IO.File.Copy(file, destFile, overwrite: true);
                                migrationResults.Add($"Moved: {categoryName}/{sizeName}/{fileName}");
                                
                                _logger.LogInformation($"✅ Copied: {file} -> {destFile}");
                            }
                            catch (Exception ex)
                            {
                                var error = $"Failed to copy {file}: {ex.Message}";
                                errorResults.Add(error);
                                _logger.LogError($"❌ {error}");
                            }
                        }
                    }
                }

                // Update database URLs for resources
                var dbUpdateResults = await UpdateDatabaseImageUrls();
                migrationResults.AddRange(dbUpdateResults);

                return Ok(new
                {
                    message = "Migration completed",
                    migrated = migrationResults.Count,
                    errors = errorResults.Count,
                    details = migrationResults,
                    errorDetails = errorResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource migration");
                return StatusCode(500, new { error = $"Migration failed: {ex.Message}" });
            }
        }

        private async Task<List<string>> UpdateDatabaseImageUrls()
        {
            var updateResults = new List<string>();

            try
            {
                // Get all resources that have old image URLs
                var resources = await _websiteContext.Resources
                    .Where(r => r.ImageUrl != null && r.ImageUrl.StartsWith("/images/resources/"))
                    .ToListAsync();

                foreach (var resource in resources)
                {
                    var oldImageUrl = resource.ImageUrl;
                    var oldThumbnailUrl = resource.ThumbnailUrl;

                    // Update ImageUrl: /images/resources/article/full/filename.webp -> /api/resources/images/article/full/filename.webp
                    if (!string.IsNullOrEmpty(resource.ImageUrl))
                    {
                        resource.ImageUrl = resource.ImageUrl.Replace("/images/resources/", "/api/resources/images/");
                    }

                    // Update ThumbnailUrl
                    if (!string.IsNullOrEmpty(resource.ThumbnailUrl))
                    {
                        resource.ThumbnailUrl = resource.ThumbnailUrl.Replace("/images/resources/", "/api/resources/images/");
                    }

                    updateResults.Add($"Updated DB: Resource ID {resource.Id} - {oldImageUrl} -> {resource.ImageUrl}");
                    _logger.LogInformation($"✅ Updated Resource ID {resource.Id}: {oldImageUrl} -> {resource.ImageUrl}");
                }

                await _websiteContext.SaveChangesAsync();
                updateResults.Add($"Updated {resources.Count} database records");
            }
            catch (Exception ex)
            {
                updateResults.Add($"Database update failed: {ex.Message}");
                _logger.LogError(ex, "Failed to update database URLs");
            }

            return updateResults;
        }

        [HttpPost("migrate-existing-company-logos")]
        public async Task<IActionResult> MigrateExistingCompanyLogos()
        {
            try
            {
                var migrationResults = new List<string>();
                var errorResults = new List<string>();

                // Source path: wwwroot/uploads/logos
                var sourceBasePath = Path.Combine(_env.WebRootPath, "uploads", "logos");
                
                // Destination path: External configured path
                var destBasePath = _filePathConfig.CompanyLogosFolder;

                // Ensure destination directory exists
                Directory.CreateDirectory(destBasePath);

                if (!Directory.Exists(sourceBasePath))
                {
                    return Ok(new { 
                        message = "No existing company logos folder found to migrate",
                        migrated = 0,
                        errors = 0
                    });
                }

                // Get all logo files
                var logoFiles = Directory.GetFiles(sourceBasePath);

                foreach (var file in logoFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(file);
                        var destFile = Path.Combine(destBasePath, fileName);

                        // Copy file to external location
                        System.IO.File.Copy(file, destFile, overwrite: true);
                        migrationResults.Add($"Moved logo: {fileName}");
                        
                        _logger.LogInformation($"✅ Copied logo: {file} -> {destFile}");
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to copy {file}: {ex.Message}";
                        errorResults.Add(error);
                        _logger.LogError($"❌ {error}");
                    }
                }

                // Update database URLs for company logos
                var dbUpdateResults = await UpdateDatabaseLogoUrls();
                migrationResults.AddRange(dbUpdateResults);

                return Ok(new
                {
                    message = "Company logo migration completed",
                    migrated = migrationResults.Count,
                    errors = errorResults.Count,
                    details = migrationResults,
                    errorDetails = errorResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during company logo migration");
                return StatusCode(500, new { error = $"Logo migration failed: {ex.Message}" });
            }
        }

        private async Task<List<string>> UpdateDatabaseLogoUrls()
        {
            var updateResults = new List<string>();

            try
            {
                // Get all companies that have old logo URLs
                var companies = await _appContext.PartyInfos
                    .Where(p => p.LogoPath != null && p.LogoPath.StartsWith("/uploads/logos/"))
                    .ToListAsync();

                foreach (var company in companies)
                {
                    var oldLogoPath = company.LogoPath;

                    // Update LogoPath: /uploads/logos/filename.jpg -> /api/companies/logos/filename.jpg
                    if (!string.IsNullOrEmpty(company.LogoPath))
                    {
                        company.LogoPath = company.LogoPath.Replace("/uploads/logos/", "/api/companies/logos/");
                    }

                    updateResults.Add($"Updated DB: Company ID {company.PartyInfoId} ({company.CompanyName}) - {oldLogoPath} -> {company.LogoPath}");
                    _logger.LogInformation($"✅ Updated Company ID {company.PartyInfoId}: {oldLogoPath} -> {company.LogoPath}");
                }

                await _appContext.SaveChangesAsync();
                updateResults.Add($"Updated {companies.Count} company logo database records");
            }
            catch (Exception ex)
            {
                updateResults.Add($"Database logo update failed: {ex.Message}");
                _logger.LogError(ex, "Failed to update database logo URLs");
            }

            return updateResults;
        }

        [HttpPost("cleanup-old-images")]
        public IActionResult CleanupOldImages()
        {
            try
            {
                var sourceBasePath = Path.Combine(_env.WebRootPath, "images", "resources");
                
                if (!Directory.Exists(sourceBasePath))
                {
                    return Ok(new { message = "No old resources folder found to cleanup" });
                }

                // Delete the entire resources folder from wwwroot
                Directory.Delete(sourceBasePath, recursive: true);
                
                _logger.LogInformation($"✅ Cleaned up old resources folder: {sourceBasePath}");
                
                return Ok(new { 
                    message = "Old resources folder cleaned up successfully",
                    deletedPath = sourceBasePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                return StatusCode(500, new { error = $"Cleanup failed: {ex.Message}" });
            }
        }

        [HttpPost("cleanup-old-company-logos")]
        public IActionResult CleanupOldCompanyLogos()
        {
            try
            {
                var sourceBasePath = Path.Combine(_env.WebRootPath, "uploads", "logos");
                
                if (!Directory.Exists(sourceBasePath))
                {
                    return Ok(new { message = "No old company logos folder found to cleanup" });
                }

                // Delete the entire logos folder from wwwroot
                Directory.Delete(sourceBasePath, recursive: true);
                
                _logger.LogInformation($"✅ Cleaned up old company logos folder: {sourceBasePath}");
                
                return Ok(new { 
                    message = "Old company logos folder cleaned up successfully",
                    deletedPath = sourceBasePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during company logos cleanup");
                return StatusCode(500, new { error = $"Company logos cleanup failed: {ex.Message}" });
            }
        }
    }
}