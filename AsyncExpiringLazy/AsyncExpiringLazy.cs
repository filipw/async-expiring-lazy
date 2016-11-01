using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncExpiringLazy
{
    public class AsyncExpiringLazy<T>
    {
        private static readonly SemaphoreSlim SyncLock = new SemaphoreSlim(initialCount: 1);
        private readonly Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> _valueProvider;
        private DateTimeOffset? _validUntil;
        private ExpirationMetadata<T> _value;

        public AsyncExpiringLazy(Func<ExpirationMetadata<T>, Task<ExpirationMetadata<T>>> valueProvider)
        {
            if (valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
            _valueProvider = valueProvider;
        }

        public bool IsValueCreated => _validUntil != null && _validUntil > DateTimeOffset.UtcNow && _value.Result != null;

        public async Task<T> Value()
        {
            if (!IsValueCreated)
            {
                await SyncLock.WaitAsync();
                try
                {
                    var result = await _valueProvider(_value).ConfigureAwait(false);
                    _value = result;
                    _validUntil = result.ValidUntil;
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
            _validUntil = null;
        }
    }
}
