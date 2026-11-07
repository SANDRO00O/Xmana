using System;
using System.Threading;
using Windows.UI.Core;

namespace Xmana.App.Services
{
    /// <summary>
    /// Coalesces change notifications (which may arrive on any thread, in bursts) into at most one pending render on the UI
    /// thread. After <see cref="Stop"/> nothing renders again, so a page that has been left never touches stale controls.
    /// </summary>
    internal sealed class RenderScheduler
    {
        private readonly CoreDispatcher _dispatcher;
        private readonly Action _render;
        private int _queued;
        private volatile bool _active = true;

        public RenderScheduler(CoreDispatcher dispatcher, Action render)
        {
            _dispatcher = dispatcher;
            _render = render;
        }

        public void Request()
        {
            if (!_active || Interlocked.Exchange(ref _queued, 1) == 1)
            {
                return;
            }

            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Interlocked.Exchange(ref _queued, 0);
                if (_active)
                {
                    _render();
                }
            });
        }

        public void Stop() => _active = false;
    }
}
