using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace eInvWorld.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SystemHealthModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly FilePathConfig _paths;
        private readonly IWebHostEnvironment _env;

        public SystemHealthModel(ApplicationDbContext db, IConfiguration config,
            IOptions<FilePathConfig> paths, IWebHostEnvironment env)
        {
            _db = db;
            _config = config;
            _paths = paths.Value;
            _env = env;
        }

        // App
        public string Version => _config["AppInfo:Version"] ?? "—";
        public string Environment => _env.EnvironmentName;
        public string Runtime => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        // Background jobs
        public int QueuedJobs { get; private set; }
        public int RunningJobs { get; private set; }
        public int FailedJobs { get; private set; }
        public string OldestQueuedAge { get; private set; } = "—";

        // Stores
        public long AuditRows { get; private set; }
        public int SubmissionRecords { get; private set; }

        // Infra
        public string KeyRingStatus { get; private set; } = "—";
        public string DocumentsDisk { get; private set; } = "—";
        public string SigningCert { get; private set; } = "—";

        public async Task OnGetAsync()
        {
            await LoadJobsAsync();
            await LoadStoresAsync();
            LoadKeyRing();
            LoadDisk();
            LoadSigningCert();
        }

        private async Task LoadJobsAsync()
        {
            try
            {
                var jobs = _db.Set<SyncJob>().AsNoTracking();
                QueuedJobs = await jobs.CountAsync(j => j.Status == SyncJobStatus.Queued);
                RunningJobs = await jobs.CountAsync(j => j.Status == SyncJobStatus.Running);
                FailedJobs = await jobs.CountAsync(j => j.Status == SyncJobStatus.Failed);

                var oldest = await jobs.Where(j => j.Status == SyncJobStatus.Queued)
                    .OrderBy(j => j.QueuedAtUtc)
                    .Select(j => (DateTime?)j.QueuedAtUtc)
                    .FirstOrDefaultAsync();
                OldestQueuedAge = oldest is null ? "none" : Ago(DateTime.UtcNow - oldest.Value);
            }
            catch (Exception ex) { OldestQueuedAge = "unavailable (" + ex.GetType().Name + ")"; }
        }

        private async Task LoadStoresAsync()
        {
            try { AuditRows = await _db.Set<eInvWorld.Models.Audit.AuditLog>().AsNoTracking().LongCountAsync(); }
            catch { AuditRows = -1; }

            try { SubmissionRecords = await _db.Set<SubmissionRecord>().AsNoTracking().CountAsync(); }
            catch { SubmissionRecords = -1; }
        }

        private void LoadKeyRing()
        {
            var path = _config["DataProtection:KeyRingPath"];
            if (string.IsNullOrWhiteSpace(path)) { KeyRingStatus = "in-app fallback (set DataProtection:KeyRingPath on a server)"; return; }
            try
            {
                if (!Directory.Exists(path)) { KeyRingStatus = "MISSING: " + path; return; }
                var probe = Path.Combine(path, $".health-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                var keys = Directory.GetFiles(path, "*.xml").Length;
                KeyRingStatus = $"OK ({keys} key file(s)) — {path}";
            }
            catch (Exception ex) { KeyRingStatus = $"NOT writable ({ex.GetType().Name}) — {path}"; }
        }

        private void LoadDisk()
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(_paths.BasePath));
                if (string.IsNullOrEmpty(root)) { DocumentsDisk = "unknown"; return; }
                var d = new DriveInfo(root);
                DocumentsDisk = $"{Bytes(d.AvailableFreeSpace)} free of {Bytes(d.TotalSize)} on {d.Name}";
            }
            catch (Exception ex) { DocumentsDisk = "unavailable (" + ex.GetType().Name + ")"; }
        }

        private void LoadSigningCert()
        {
            if (!_config.GetValue("LHDNApiConfig:SigningEnabled", false)) { SigningCert = "signing disabled"; return; }
            var certPath = _config["LHDNApiConfig:CertPath"];
            if (string.IsNullOrWhiteSpace(certPath)) { SigningCert = "enabled but CertPath is empty"; return; }
            try
            {
                var full = Path.IsPathRooted(certPath) ? certPath : Path.Combine(_env.ContentRootPath, certPath);
                if (!System.IO.File.Exists(full)) { SigningCert = "enabled but file missing: " + full; return; }
                // X509CertificateLoader is the non-obsolete loader on .NET 9+ (matches DocumentSigningService).
                using var cert = X509CertificateLoader.LoadPkcs12FromFile(full, _config["LHDNApiConfig:CertPass"]);
                var days = (cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;
                var state = days < 0 ? "EXPIRED" : days < 30 ? "expiring soon" : "valid";
                SigningCert = $"{state}: expires {cert.NotAfter:yyyy-MM-dd} ({days:F0} day(s))";
            }
            catch (Exception ex) { SigningCert = "could not read cert (" + ex.GetType().Name + ")"; }
        }

        private static string Ago(TimeSpan t) =>
            t.TotalMinutes < 1 ? "just now" :
            t.TotalHours < 1 ? $"{t.TotalMinutes:F0} min" :
            t.TotalDays < 1 ? $"{t.TotalHours:F1} h" : $"{t.TotalDays:F1} d";

        private static string Bytes(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:F1} {u[i]}";
        }
    }
}
