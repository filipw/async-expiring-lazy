using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Strathweb
{
    class BackgroundMonitor<T> : IDisposable
    {
        private readonly Func<CancellationToken, Task<ExpirationMetadata<T>>> _factory;
        private readonly Action<ExpirationMetadata<T>> _onNewItem;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _monitorTask;
        private int _monitorStatus;

        public BackgroundMonitor(Func<CancellationToken, Task<ExpirationMetadata<T>>> factory,
            Action<ExpirationMetadata<T>> onNewItem)
        {
            _factory = factory;
            _onNewItem = onNewItem;
            _monitorTask = new Task(async () => await RunMonitor(_cts.Token).ConfigureAwait(false));
        }

        public void StartIfNotStarted()
        {
            if (Interlocked.CompareExchange(ref _monitorStatus, 1, 0) == 0)
            {
                _monitorTask.Start();
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _monitorStatus, -1, 1) == 1)
            {
                _cts.Cancel();
            }
        }

        private async Task RunMonitor(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Pass the cancellation token to the factory method
                    var metadata = await _factory(ct).ConfigureAwait(false);
                    if (metadata.Result == null) continue;

                    NotifyAboutNewItem(metadata);

                    var expiresIn = metadata.ExpiresIn;
                    if (expiresIn > TimeSpan.Zero)
                    {
                        Debug.WriteLine($"Sleeping the connection monitor for {expiresIn.TotalSeconds} seconds");
                        await Task.Delay(expiresIn, ct).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Task cancellation was requested
                }
                catch (ObjectDisposedException)
                {
                    // Object has been disposed, exit gracefully
                }
            }

            Debug.WriteLine("Cancellation requested, stopping background monitor");
        }

        private void NotifyAboutNewItem(ExpirationMetadata<T> metadata) => _onNewItem(metadata);

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}