using System;
using System.Collections.Generic;
using System.Linq;
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
            if (valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
            _valueProvider = valueProvider;
        }

        public bool IsValueCreated => _value.Result != null && _value.ValidUntil > DateTimeOffset.UtcNow;

        public async Task<T> Value()
        {
            if (!IsValueCreated)
            {
                await _syncLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var result = await _valueProvider(_value).ConfigureAwait(false);
                    _value = result;
                }
                finally
                {
                    _syncLock.Release();
                }
            }

            return _value.Result;
        }

        public void Invalidate()
        {
            _syncLock.Wait();
            _value = default(ExpirationMetadata<T>);
            _syncLock.Release();
        }
    }
}
