using System;
using System.Diagnostics;
using System.IO;

namespace ToLaunch.Services;

/// <summary>
/// Simple logging service that writes messages to an error.log file in the executable directory.
/// </summary>
public static class LogService
{
    private static readonly object _lock = new();
    private static readonly string _logFilePath;
    private static bool _initialized = false;

    static LogService()
    {
        var exeDirectory = AppContext.BaseDirectory;
        _logFilePath = Path.Combine(exeDirectory, "error.log");
    }

    /// <summary>
    /// Initializes the log service and sets up trace listeners.
    /// Call this once at application startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        try
        {
            // Clean up the log file from previous session
            ClearLogFile();

            // Add a trace listener that writes to our log file
            Trace.Listeners.Add(new LogTraceListener(_logFilePath));
            _initialized = true;

            // Log startup
            LogInfo($"ToLaunch started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            // If we can't initialize logging, write to console
            Console.WriteLine($"Failed to initialize logging: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the log file. Called at application startup.
    /// </summary>
    private static void ClearLogFile()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }
        catch
        {
            // Silently fail if we can't delete the log file
        }
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void LogWarning(string message)
    {
        WriteLog("WARN", message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    /// <summary>
    /// Logs an error message with exception details.
    /// </summary>
    public static void LogError(string message, Exception ex)
    {
        WriteLog("ERROR", $"{message}: {ex.Message}");
        if (ex.StackTrace != null)
        {
            WriteLog("ERROR", $"StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public static void LogDebug(string message)
    {
        WriteLog("DEBUG", message);
    }

    private static void WriteLog(string level, string message)
    {
        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }

            // Also write to debug output
            Debug.WriteLine(logLine);
        }
        catch
        {
            // Silently fail if we can't write to log
        }
    }

    /// <summary>
    /// Gets the path to the log file.
    /// </summary>
    public static string LogFilePath => _logFilePath;
}

/// <summary>
/// Custom trace listener that writes to the log file.
/// </summary>
internal class LogTraceListener : TraceListener
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LogTraceListener(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public override void Write(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, message);
            }
        }
        catch
        {
            // Silently fail
        }
    }

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DEBUG] {message}";
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }
        }
        catch
        {
            // Silently fail
        }
    }
}
