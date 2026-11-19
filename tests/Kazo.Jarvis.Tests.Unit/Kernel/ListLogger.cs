using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Kernel;

/// <summary>Keeps the log entries in memory, to check that an error or an ignored input is logged.</summary>
internal sealed class ListLogger : ILogger, ILoggerFactory, ILoggerProvider
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Entries)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    public ILogger CreateLogger(string categoryName) => this;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
