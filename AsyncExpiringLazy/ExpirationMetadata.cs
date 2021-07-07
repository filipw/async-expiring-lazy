﻿using System;

namespace Strathweb
{
    public struct ExpirationMetadata<T>
    {
        public T Result { get; set; }

        public DateTimeOffset ValidUntil { get; set; }

        public TimeSpan ExpiresIn => ValidUntil - DateTimeOffset.UtcNow;
    }
}