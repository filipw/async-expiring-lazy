using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsyncExpiringLazy;

namespace Strathweb
{
    /// <summary>
    /// Eager implementation of the <see cref="AsyncExpiringLazy{T}"/> which uses a background worker to refresh the item.
    /// It performs better in the situations when factory method is slow and having a fast access to the fresh and ready T item is important.
    /// The background worker is started after the first call to the <see cref="Value"/> method.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncExpiringEager<T> : IDisposable
    {
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private ExpirationMetadata<T> _value;

        private readonly BackgroundMonitor<T> _monitor;
        private readonly object _lock = new object();
        private readonly AsyncManualResetEvent _itemPrepared = new AsyncManualResetEvent();
        private bool _disposed;

        public AsyncExpiringEager(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider)
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
                throw new ObjectDisposedException(nameof(AsyncExpiringEager<T>), "Background monitor is disposed");
            }

            _monitor.StartIfNotStarted();
            await _itemPrepared.WaitAsync().ConfigureAwait(false);

            lock (_lock)
            {
                if (_value.ExpiresIn > TimeSpan.Zero)
                {
                    return _value.Result;
                }
            }

            Debug.WriteLine("Background monitor was not able to provide usable item, creating new manually");
            var newItem = await GetNewItem().ConfigureAwait(false);
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

            try
            {
                return await _valueProvider(copy).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                OnException(e);
                return default;
            }
        }

        private void OnException(Exception e)
        {
            lock (_lock)
            {
                _monitor.Stop();

                if (_itemPrepared.IsSet) 
                    _itemPrepared.Reset();
                
                _itemPrepared.SetException(e);
            }
        }

        private void OnNewItem(ExpirationMetadata<T> newItem)
        {
            lock (_lock)
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
