namespace OwnerKeeper.Core.Logging;

/// <summary>Severity levels for logging.</summary>
public enum LogLevel
{
    /// <summary>Informational messages.</summary>
    Info,

    /// <summary>Recoverable or noteworthy conditions.</summary>
    Warning,

    /// <summary>Errors or failed operations.</summary>
    Error,
}

/// <summary>Simple logger contract.</summary>
public interface ILogger
{
    /// <summary>Write a log with level and message.</summary>
    void Log(LogLevel level, string message);
}

/// <summary>
/// Default console logger. Wraps Console.WriteLine with a simple prefix.
/// (REQ-LG-001)
/// </summary>
public sealed class ConsoleLogger : ILogger
{
    /// <summary>Write a message to the console.</summary>
    public void Log(LogLevel level, string message) =>
        System.Console.WriteLine($"[{level}] {message}");
}
