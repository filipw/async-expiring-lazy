﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Strathweb;

namespace AsyncExpiringLazy
{
    class BackgroundMonitor<T> : IDisposable
    {
        private readonly Func<Task<ExpirationMetadata<T>>> _factory;
        private readonly Action<ExpirationMetadata<T>> _onNewItem;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _monitorTask;
        private int _monitorStatus;

        public BackgroundMonitor(Func<Task<ExpirationMetadata<T>>> factory, Action<ExpirationMetadata<T>> onNewItem)
        {
            _factory = factory;
            _onNewItem = onNewItem;
            _monitorTask = new Task(async () => await RunMonitor().ConfigureAwait(false));
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

        private async Task RunMonitor()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var metadata = await _factory().ConfigureAwait(false);
                    if (metadata.Result == null) continue;
                    NotifyAboutNewItem(metadata);

                    var expiresIn = metadata.ExpiresIn;
                    if (expiresIn > TimeSpan.Zero)
                    {
                        Debug.WriteLine($"Sleeping the connection monitor for {expiresIn.TotalSeconds} seconds");
                        await Task.Delay(expiresIn, _cts.Token).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
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
