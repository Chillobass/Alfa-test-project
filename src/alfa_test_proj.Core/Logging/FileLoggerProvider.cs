using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _disposed;

    public FileLoggerProvider(string directory, LogLevel minLevel = LogLevel.Information)
    {
        _directory = directory;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(ShortLevel(level)).Append("] ")
            .Append(ShortCategory(category)).Append(": ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            var today = DateOnly.FromDateTime(now);
            if (_writer is null || _writerDate != today)
            {
                _writer?.Dispose();
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, $"app-{now:yyyyMMdd}.log");
                _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(false)) { AutoFlush = true };
                _writerDate = today;
            }

            _writer.WriteLine(line.ToString());
        
        }
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    private static string ShortCategory(string category)
    {
        var dot = category.LastIndexOf('.');
        return dot >= 0 ? category[(dot + 1)..] : category;
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider._minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string directory,
        LogLevel minLevel = LogLevel.Information)
    {
        return builder.AddProvider(new FileLoggerProvider(directory, minLevel));
    }
}
