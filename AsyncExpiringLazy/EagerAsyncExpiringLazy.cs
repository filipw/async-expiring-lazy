using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncExpiringLazy;

namespace Strathweb
{
    public class EagerAsyncExpiringLazy<T> : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private readonly EagerAsyncExpiringLazyOptions _options;
        private ExpirationMetadata<T> _value;

        private readonly AsyncManualResetEvent _resetEvent = new AsyncManualResetEvent();
        private readonly Task _valueMonitor;
        private int _valueMonitorStarted;

        public EagerAsyncExpiringLazy(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider, EagerAsyncExpiringLazyOptions options)
        {
            if (valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
            _valueProvider = valueProvider;
            _options = options;
            _valueMonitor = new Task(async () => await MonitorValueExpiration());
        }

        private async Task MonitorValueExpiration()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_value.Result == null || IsValueExpiringSoonInternal)
                    {
                        var result = await _valueProvider(_value).ConfigureAwait(false);
                        _resetEvent.Reset();
                        _value = result;
                        _resetEvent.Set();
                    }

                    var checkAgainIn = CalculateNextIterationTime(_value);
                    await Task.Delay(checkAgainIn, _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private TimeSpan CalculateNextIterationTime(ExpirationMetadata<T> metadata)
        {
            var unusableIn = metadata.ValidUntil - DateTimeOffset.UtcNow;
            var alreadyNeedsRefresh = unusableIn < _options.MinimumRemainingTime;
            return alreadyNeedsRefresh ? TimeSpan.FromTicks(1) : unusableIn;
        }

        private bool IsValueExpiringSoonInternal =>
            _value.Result != null &&
            _value.ValidUntil - DateTimeOffset.UtcNow < _options.MinimumRemainingTime;

        public bool IsValueCreated() => _resetEvent.IsSet;

        public async Task<T> Value()
        {
            if (Interlocked.CompareExchange(ref _valueMonitorStarted, 1, 0) == 0)
            {
                _valueMonitor.Start();
            }

            await _resetEvent.WaitAsync();
            return _value.Result;
        }

        public void Invalidate()
        {
            _resetEvent.Reset();
            _value = default;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            Invalidate();
        }
    }
}
