using System;

namespace Strathweb
{
    public class EagerAsyncExpiringLazyOptions
    {
        public TimeSpan MinimumRemainingTime { get; set; } = TimeSpan.FromSeconds(20);
    }
}