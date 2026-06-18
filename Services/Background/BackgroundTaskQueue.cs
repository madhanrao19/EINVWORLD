using System.Collections.Concurrent;
using System.Threading.Channels;

namespace EINVWORLD.Services.Background
{
    public interface IBackgroundTaskQueue
    {
        // Pass the TIN/TenantId along with the work item
        ValueTask EnqueueAsync(string tin, Func<CancellationToken, Task> workItem);

        ValueTask<Func<CancellationToken, Task>?> DequeueAsync(CancellationToken ct);
    }

    public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        // Dictionary of queues, one per TIN
        private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<CancellationToken, Task>>> _tenantQueues = new();
        private readonly List<string> _activeTenants = new();
        private int _currentTenantIndex = 0;
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public ValueTask EnqueueAsync(string tin, Func<CancellationToken, Task> workItem)
        {
            if (workItem is null) throw new ArgumentNullException(nameof(workItem));

            var queue = _tenantQueues.GetOrAdd(tin, _ => new ConcurrentQueue<Func<CancellationToken, Task>>());

            queue.Enqueue(workItem);

            // Ensure this TIN is in the active rotation on EVERY enqueue. A drained TIN is removed
            // from _activeTenants by DequeueAsync (but its queue entry stays), so registering only
            // inside the GetOrAdd factory would silently drop the 2nd+ job for the same TIN.
            lock (_activeTenants)
            {
                if (!_activeTenants.Contains(tin)) _activeTenants.Add(tin);
            }

            _signal.Release(); // Signal that work is available

            return ValueTask.CompletedTask;
        }

        public async ValueTask<Func<CancellationToken, Task>?> DequeueAsync(CancellationToken ct)
        {
            await _signal.WaitAsync(ct);

            lock (_activeTenants)
            {
                // Round-robin logic: cycle through active tenants
                for (int i = 0; i < _activeTenants.Count; i++)
                {
                    _currentTenantIndex = (_currentTenantIndex + 1) % _activeTenants.Count;
                    string currentTin = _activeTenants[_currentTenantIndex];

                    if (_tenantQueues.TryGetValue(currentTin, out var queue) && queue.TryDequeue(out var workItem))
                    {
                        // If this tenant's queue is empty, remove them from active rotation
                        if (queue.IsEmpty)
                        {
                            _activeTenants.RemoveAt(_currentTenantIndex);
                            _currentTenantIndex--; // Adjust index after removal
                        }
                        return workItem;
                    }
                }
            }
            return null;
        }
    }
}
