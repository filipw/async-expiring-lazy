using System.Threading.Tasks;

namespace AsyncExpiringLazy
{
    sealed class AsyncManualResetEvent
    {
        private readonly object _mutex;
        private TaskCompletionSource<object> _tcs;

        public AsyncManualResetEvent()
        {
            _mutex = new object();
            _tcs = new TaskCompletionSource<object>();
        }

        public bool IsSet
        {
            get { lock (_mutex) return _tcs.Task.IsCompleted; }
        }

        public Task WaitAsync()
        {
            lock (_mutex)
            {
                return _tcs.Task;
            }
        }

        public void Set()
        {
            lock (_mutex)
            {
                _tcs.TrySetResult(null);
            }
        }

        public void Reset()
        {
            lock (_mutex)
            {
                if (_tcs.Task.IsCompleted)
                    _tcs = new TaskCompletionSource<object>();
            }
        }
    }
}
