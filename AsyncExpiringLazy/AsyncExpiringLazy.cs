using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Strathweb
{
    public class AsyncExpiringLazy<T>
    {
        private static readonly SemaphoreSlim SyncLock = new SemaphoreSlim(initialCount: 1);
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
                await SyncLock.WaitAsync();
                try
                {
                    var result = await _valueProvider(_value).ConfigureAwait(false);
                    _value = result;
                }
                finally
                {
                    SyncLock.Release();
                }
            }

            return _value.Result;
        }

        public void Invalidate()
        {
            _value = default(ExpirationMetadata<T>);
        }
    }
}
