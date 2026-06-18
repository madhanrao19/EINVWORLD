using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Services.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace eInvWorld.Services
{
    public class JsonFileService : IJsonFileService
    {
        private readonly FilePathConfig _filePathConfig;
        private readonly ILogger<JsonFileService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly InvoiceHistoryService _invoiceHistoryService;


        public JsonFileService(
             IOptions<FilePathConfig> filePathConfig,
             ILogger<JsonFileService> logger,
             ApplicationDbContext context,
             IHttpContextAccessor httpContextAccessor,
             InvoiceHistoryService invoiceHistoryService)
        {
            _filePathConfig = filePathConfig.Value;
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _invoiceHistoryService = invoiceHistoryService;

        }


        public void MoveToStatusFolder(string invoiceNo, string status)
        {
            try
            {
                var sourcePath = Path.Combine(_filePathConfig.DraftFolder, $"{invoiceNo}.json");

                var targetFolder = status switch
                {
                    "Valid" => _filePathConfig.ValidFolder,
                    "Invalid" => _filePathConfig.InvalidFolder,
                    "Submitted" => _filePathConfig.SubmittedFolder,
                    "Cancelled" => _filePathConfig.CancelledFolder,
                    _ => _filePathConfig.DraftFolder
                };

                var targetPath = Path.Combine(targetFolder, $"{invoiceNo}.json");

                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, targetPath, true);
                    _logger.LogInformation("[Success] Moved {InvoiceNo}.json to {Status} folder", invoiceNo, status);

                    // ✅ Log movement for Admins only
                    if (UserIsInRole("Admin"))
                    {
                        var fromFolder = _filePathConfig.DraftFolder;
                        _invoiceHistoryService.Log(
                            invoiceNo,
                            "FileMoved",
                            $"JSON moved from '{fromFolder}' to '{targetFolder}'"
                        );
                    }
                }
                else
                {
                    _logger.LogWarning("[Skip] File not found: {Path}", sourcePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Moving JSON file failed for {InvoiceNo}", invoiceNo);
            }
        }

        private bool UserIsInRole(string role)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.IsInRole(role) ?? false;
        }

        public string? GetExistingFilePath(string invoiceNo)
        {
            var possibleFolders = new[]
            {
        _filePathConfig.DraftFolder,
        _filePathConfig.SubmittedFolder,
        _filePathConfig.ValidFolder,
        _filePathConfig.InvalidFolder,
        _filePathConfig.CancelledFolder
    };

            foreach (var folder in possibleFolders)
            {
                var fullPath = Path.Combine(folder, $"{invoiceNo}.json");
                if (File.Exists(fullPath))
                {
                    _logger.LogInformation("✅ Found {InvoiceNo}.json in: {Folder}", invoiceNo, folder);
                    return fullPath;
                }
            }

            _logger.LogWarning("❌ JSON file for Invoice {InvoiceNo} not found in any known folder.", invoiceNo);
            return null;
        }

    }
}
