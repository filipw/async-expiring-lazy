using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strathweb
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

        public bool IsFaulted
        {
            get { lock (_mutex) return _tcs.Task.IsFaulted; }
        }

        public Task WaitAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);
            
            lock (_mutex)
            {
                if (_tcs.Task.IsCompleted)
                    return _tcs.Task;
                
                var cancellationCts = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (ct.Register(() => cancellationCts.TrySetCanceled(ct)))
                {
                    return Task.WhenAny(_tcs.Task, cancellationCts.Task).Unwrap();
                }
            }
        }

        public void Set()
        {
            lock (_mutex)
            {
                _tcs.TrySetResult(null);
            }
        }

        public void SetException(Exception e)
        {
            lock (_mutex)
            {
                _tcs.TrySetException(e);
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
