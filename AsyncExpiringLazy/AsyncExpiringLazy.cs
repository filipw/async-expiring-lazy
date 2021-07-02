using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strathweb
{
    public class AsyncExpiringLazy<T>
    {
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(initialCount: 1);
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private ExpirationMetadata<T> _value;

        public AsyncExpiringLazy(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider)
        {
            _valueProvider = valueProvider ?? throw new ArgumentNullException(nameof(valueProvider));
        }

        private bool IsValueCreatedInternal => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

        public async Task<bool> IsValueCreated()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return IsValueCreatedInternal;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<T> Value()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsValueCreatedInternal)
                {
                    return _value.Result;
                }

                var result = await _valueProvider(_value).ConfigureAwait(false);
                _value = result;
                return _value.Result;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task Invalidate()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            _value = default(ExpirationMetadata<T>);
            _syncLock.Release();
        }
    }
}
