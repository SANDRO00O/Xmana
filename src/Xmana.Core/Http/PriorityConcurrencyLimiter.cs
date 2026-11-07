using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Http
{
    /// <summary>
    /// Request priority (PROMPT.md section 57). Lower numeric value is served first:
    /// hero, continue watching, primary rails, secondary rails, everything else.
    /// </summary>
    public enum RequestPriority
    {
        Hero = 0,
        ContinueWatching = 1,
        Primary = 2,
        Secondary = 3,
        Low = 4,
    }

    /// <summary>
    /// Caps simultaneous in-flight requests and hands free slots to the most important waiter first
    /// (FIFO within the same priority). A waiter whose token is cancelled leaves the queue immediately.
    /// </summary>
    public sealed class PriorityConcurrencyLimiter
    {
        private readonly object _gate = new object();
        private readonly int _maxConcurrent;
        private readonly List<Waiter> _waiters = new List<Waiter>();
        private int _active;
        private long _sequence;

        public PriorityConcurrencyLimiter(int maxConcurrent)
        {
            if (maxConcurrent < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
            }

            _maxConcurrent = maxConcurrent;
        }

        public int ActiveCount
        {
            get
            {
                lock (_gate)
                {
                    return _active;
                }
            }
        }

        public int WaitingCount
        {
            get
            {
                lock (_gate)
                {
                    return _waiters.Count;
                }
            }
        }

        /// <summary>Waits for a slot. Dispose the returned lease to free it.</summary>
        public async Task<IDisposable> AcquireAsync(RequestPriority priority, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Waiter waiter;
            lock (_gate)
            {
                if (_active < _maxConcurrent && _waiters.Count == 0)
                {
                    _active++;
                    return new Lease(this);
                }

                waiter = new Waiter(priority, _sequence++);
                _waiters.Add(waiter);
            }

            CancellationTokenRegistration registration = default(CancellationTokenRegistration);
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    bool removed;
                    lock (_gate)
                    {
                        removed = _waiters.Remove(waiter);
                    }

                    if (removed)
                    {
                        waiter.Completion.TrySetCanceled();
                    }
                });
            }

            try
            {
                await waiter.Completion.Task.ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
            }

            return new Lease(this);
        }

        private void Release()
        {
            Waiter? next = null;
            lock (_gate)
            {
                if (_waiters.Count > 0)
                {
                    var best = 0;
                    for (var i = 1; i < _waiters.Count; i++)
                    {
                        if (IsBefore(_waiters[i], _waiters[best]))
                        {
                            best = i;
                        }
                    }

                    next = _waiters[best];
                    _waiters.RemoveAt(best);
                    // The slot is handed over directly: _active stays unchanged.
                }
                else
                {
                    _active--;
                }
            }

            if (next != null)
            {
                next.Completion.TrySetResult(true);
            }
        }

        private static bool IsBefore(Waiter a, Waiter b) =>
            a.Priority < b.Priority || (a.Priority == b.Priority && a.Sequence < b.Sequence);

        private sealed class Waiter
        {
            public Waiter(RequestPriority priority, long sequence)
            {
                Priority = priority;
                Sequence = sequence;
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public RequestPriority Priority { get; }

            public long Sequence { get; }

            public TaskCompletionSource<bool> Completion { get; }
        }

        private sealed class Lease : IDisposable
        {
            private PriorityConcurrencyLimiter? _owner;

            public Lease(PriorityConcurrencyLimiter owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null)
                {
                    owner.Release();
                }
            }
        }
    }
}
