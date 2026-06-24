using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Services;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Services.Background
{
    public class TokenRenewalService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenRenewalService> _logger;

        // FIX 1: Changed sleep interval from 10 minutes to 50 minutes to dramatically reduce background API spam
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(50);

        public TokenRenewalService(IServiceProvider serviceProvider, ILogger<TokenRenewalService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🟢 TokenRenewalService started. Running every {Minutes} minutes.", Interval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

                    // FIX 2: Changed look-ahead buffer from 10 minutes to 60 minutes
                    // This guarantees any token expiring before the next 50-minute sleep cycle wakes up is caught and renewed now.
                    var tokensToRenew = await dbContext.LHDNTokens
                        .Where(t => t.ExpiryTime <= DateTime.UtcNow.AddMinutes(60))
                        .ToListAsync(stoppingToken);

                    if (!tokensToRenew.Any())
                    {
                        await Task.Delay(Interval, stoppingToken);
                        continue;
                    }

                    foreach (var token in tokensToRenew)
                    {
                        try
                        {
                            // Skip and Delete garbage TINs like "NA"/empty, or any of the general TINs
                            // (exact match via the helper — not a fragile substring Contains).
                            if (string.IsNullOrWhiteSpace(token.TIN) || token.TIN == "NA" || GeneralTINHelper.IsGeneralTIN(token.TIN))
                            {
                                _logger.LogInformation("🗑️ Cleaning up invalid/general TIN '{TIN}' from LHDNTokens table.", token.TIN);
                                dbContext.LHDNTokens.Remove(token);
                                await dbContext.SaveChangesAsync(stoppingToken);
                                continue; // Skip the API call
                            }

                            _logger.LogInformation("🔄 Renewing token for TIN {TIN}", token.TIN);

                            // This will now throw immediately on 400 errors thanks to the change in TokenService
                            await tokenService.GetAccessTokenForTIN(token.TIN);

                            // Space out renewals: the /connect/token endpoint is limited to ~12 req/min.
                            // Renewing many TINs back-to-back would burst past that and trip a 429.
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        }
                        // Catch the permanent rejection and delete the token so it doesn't loop forever
                        catch (InvalidOperationException ex) when (ex.Message.Contains("rejected intermediary") || ex.Message.Contains("unauthorized_client"))
                        {
                            _logger.LogWarning("🗑️ LHDN revoked intermediary access for {TIN}. Removing from database to stop auto-renewal.", token.TIN);
                            dbContext.LHDNTokens.Remove(token);
                            try
                            {
                                await dbContext.SaveChangesAsync(stoppingToken);
                            }
                            catch (Exception saveEx)
                            {
                                // If the delete itself fails, log it — otherwise the same revoked token would
                                // be retried (and re-throw the same rejection) on every cycle, forever.
                                _logger.LogError(saveEx, "❌ Failed to delete revoked token for TIN {TIN}; it may be retried next cycle.", token.TIN);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Failed to renew token for TIN {TIN}", token.TIN);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error occurred during token renewal cycle.");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogWarning("🛑 TokenRenewalService stopped.");
        }
    }
}
