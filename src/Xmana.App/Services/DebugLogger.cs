using System.Diagnostics;
using Xmana.Core.Logging;

namespace Xmana.App.Services
{
    /// <summary>
    /// Writes to the debugger output only. Messages are already URL-redacted by the logging extensions
    /// (never signed media/image URLs). Nothing is persisted or uploaded.
    /// </summary>
    internal sealed class DebugLogger : IXmanaLogger
    {
        public void Log(LogLevel level, string message)
        {
            Debug.WriteLine("[Xmana " + level + "] " + message);
        }
    }
}
