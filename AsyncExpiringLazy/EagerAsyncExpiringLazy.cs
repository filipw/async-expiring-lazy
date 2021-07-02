using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsyncExpiringLazy;

namespace Strathweb
{
    public class EagerAsyncExpiringLazy<T> : IDisposable
    {
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private ExpirationMetadata<T> _value;

        private readonly BackgroundMonitor<T> _monitor;
        private readonly object _lock = new object();
        private readonly AsyncManualResetEvent _itemPrepared = new AsyncManualResetEvent();
        private bool _disposed;

        public EagerAsyncExpiringLazy(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider)
        {
            _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
            _monitor = new BackgroundMonitor<T>(GetNewItem, OnNewItem);
        }

        public bool IsValueCreated()
        {
            lock (_lock)
            {
                return _value.Result != null;
            }
        }

        public async Task<T> Value()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EagerAsyncExpiringLazy<T>), "Background monitor is disposed");
            }

            _monitor.StartIfNotStarted();
            await _itemPrepared.WaitAsync();

            lock (_lock)
            {
                if (_value.ValidUntil > DateTimeOffset.UtcNow)
                {
                    return _value.Result;
                }
            }

            Debug.WriteLine("Background monitor was not able to provide usable item, creating new manually");
            var newItem = await GetNewItem();
            OnNewItem(newItem);
            return newItem.Result;
        }

        private async Task<ExpirationMetadata<T>> GetNewItem()
        {
            ExpirationMetadata<T> copy;
            lock (_lock)
            {
                copy = _value;
            }
            
            return await _valueProvider(copy);
        }

        private void OnNewItem(ExpirationMetadata<T> newItem)
        {
            lock(_lock)
            {
                _value = newItem;

                if (!_itemPrepared.IsSet)
                {
                    _itemPrepared.Set();
                }
            }
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                _itemPrepared.Reset();
                _value = default;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _monitor.Dispose();
                _itemPrepared.Reset();
                _value = default;
                _disposed = true;
            }
        }
    }
}
