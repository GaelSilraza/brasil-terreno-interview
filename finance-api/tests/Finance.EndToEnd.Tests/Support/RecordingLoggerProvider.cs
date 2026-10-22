using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Finance.EndToEnd.Tests.Support;

public sealed record RecordedLogEntry(LogLevel Level, string Category, string Message);

public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogEntry> entries = new();

    public IReadOnlyCollection<RecordedLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, entries);
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string categoryName, ConcurrentQueue<RecordedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new RecordedLogEntry(logLevel, categoryName, formatter(state, exception)));
        }
    }
}
