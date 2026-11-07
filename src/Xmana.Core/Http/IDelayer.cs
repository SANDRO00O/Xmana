using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Http
{
    /// <summary>Time source for backoff waits; replaced in tests so they run instantly.</summary>
    public interface IDelayer
    {
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    public sealed class TaskDelayer : IDelayer
    {
        public static readonly TaskDelayer Instance = new TaskDelayer();

        private TaskDelayer()
        {
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
    }
}
