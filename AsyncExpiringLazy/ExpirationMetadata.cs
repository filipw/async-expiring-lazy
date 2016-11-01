using System;

namespace AsyncExpiringLazy
{
    public struct ExpirationMetadata<T>
    {
        public T Result { get; set; }

        public DateTimeOffset ValidUntil { get; set; }
    }
}