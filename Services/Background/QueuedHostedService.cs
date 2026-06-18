using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EINVWORLD.Services.Background
{
    public sealed class QueuedHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<QueuedHostedService> _log;

        public QueuedHostedService(IBackgroundTaskQueue queue, ILogger<QueuedHostedService> log)
        {
            _queue = queue;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("QueuedHostedService started");
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var work = await _queue.DequeueAsync(stoppingToken);
                    if (work != null) await work(stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Background task failed");
                }
            }

            _log.LogInformation("QueuedHostedService stopping");
        }
    }
}
