namespace GenesysAudioHookServer.Helpers;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class Logger
{
    private readonly string? _logFilePath;
    private readonly LogLevel _minLogLevel;

    public Logger(string? logFilePath = null, LogLevel minLogLevel = LogLevel.Info)
    {
        _logFilePath = logFilePath;
        _minLogLevel = minLogLevel;
    }

    private readonly object _fileLock = new();

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (level < _minLogLevel) return;

        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        Console.WriteLine(logEntry);

        if (!string.IsNullOrEmpty(_logFilePath))
        {
            lock (_fileLock)
            {
                try
                {
                    File.AppendAllTextAsync(_logFilePath, logEntry + Environment.NewLine).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}