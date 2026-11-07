namespace Xmana.Core.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Structured-ish logging sink. Messages must already be safe; use the extension methods, which
    /// redact URLs so signed media/image URLs can never reach a log.
    /// </summary>
    public interface IXmanaLogger
    {
        void Log(LogLevel level, string message);
    }

    public sealed class NullLogger : IXmanaLogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Log(LogLevel level, string message)
        {
        }
    }

    public static class LoggerExtensions
    {
        public static void Debug(this IXmanaLogger logger, string message) => logger.Log(LogLevel.Debug, LogRedactor.Redact(message));

        public static void Info(this IXmanaLogger logger, string message) => logger.Log(LogLevel.Info, LogRedactor.Redact(message));

        public static void Warn(this IXmanaLogger logger, string message) => logger.Log(LogLevel.Warning, LogRedactor.Redact(message));

        public static void Error(this IXmanaLogger logger, string message) => logger.Log(LogLevel.Error, LogRedactor.Redact(message));
    }
}
