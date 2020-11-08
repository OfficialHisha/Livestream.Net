using System;

namespace Livestream.Net
{
    public enum LogSeverity
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        EXCEPTION,
    }

    public struct LogMessage
    {
        public LogSeverity Severity { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public LogMessage(LogSeverity severity, string message, Exception exception = null)
        {
            Severity = severity;
            Message = message;
            Exception = exception;
        }
    }
}
