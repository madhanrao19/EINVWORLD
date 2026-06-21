using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EINVWORLD.Helpers.HealthChecks
{
    /// <summary>
    /// Readiness check: confirms the folders the app must write to (documents, generated PDFs, the
    /// DataProtection key ring) actually exist and are writable. On IIS this catches the common
    /// on-prem failure where the app-pool identity lacks Modify rights after a fresh deploy — the
    /// process is alive (liveness OK) but cannot do real work (readiness fails).
    /// </summary>
    public sealed class WritableFoldersHealthCheck : IHealthCheck
    {
        private readonly FilePathConfig _paths;
        private readonly IConfiguration _config;

        public WritableFoldersHealthCheck(IOptions<FilePathConfig> paths, IConfiguration config)
        {
            _paths = paths.Value;
            _config = config;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var toCheck = new Dictionary<string, string?>
            {
                ["Documents"] = _paths.BasePath,
                ["GeneratedPdf"] = _paths.GeneratedPdfFolder,
                ["DataProtectionKeys"] = _config["DataProtection:KeyRingPath"],
            };

            var failures = new List<string>();
            foreach (var (label, path) in toCheck)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue; // not configured (e.g. in-app key fallback) — not a readiness failure

                try
                {
                    if (!Directory.Exists(path))
                    {
                        failures.Add($"{label}: folder missing ({path})");
                        continue;
                    }

                    var probe = Path.Combine(path, $".health-{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                }
                catch (Exception ex)
                {
                    failures.Add($"{label}: not writable ({path}) — {ex.GetType().Name}");
                }
            }

            return Task.FromResult(failures.Count == 0
                ? HealthCheckResult.Healthy("All required folders are writable.")
                : HealthCheckResult.Unhealthy(string.Join("; ", failures)));
        }
    }
}
