using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters.Logging;

/// <summary>
/// Technical log of the Windows host: one text file per day in <c>%USERPROFILE%\.kazo\jarvis\logs</c>
/// (ADR 0004, section 1), kept <see cref="DaysKept"/> days. A desktop application has no console: without
/// this file, a failure at start-up (locked session, missing WebView2) leaves no trace.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>Number of daily files kept; older ones are deleted when the provider starts.</summary>
    public const int DaysKept = 14;

    private readonly string folder;
    private readonly TimeProvider clock;
    private readonly LogLevel minimum;
    private readonly Lock gate = new();

    /// <summary>Creates the provider and deletes the files older than <see cref="DaysKept"/> days.</summary>
    /// <param name="folder">The logs folder; created when missing.</param>
    /// <param name="clock">Gives the time of each line and the name of the day's file.</param>
    /// <param name="minimum">Lines below this level are dropped.</param>
    public FileLoggerProvider(string folder, TimeProvider clock, LogLevel minimum = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(clock);
        this.folder = folder;
        this.clock = clock;
        this.minimum = minimum;
        Directory.CreateDirectory(folder);
        DeleteOldFiles();
    }

    /// <summary>The file written today.</summary>
    public string CurrentFile => FileFor(clock.GetLocalNow());

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private string FileFor(DateTimeOffset time) =>
        Path.Combine(folder, $"jarvis-{time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = clock.GetLocalNow();
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(ShortName(level)).Append("] ")
            .Append(category).Append(": ").Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();
        lock (gate)
        {
            try
            {
                File.AppendAllText(FileFor(now), line.ToString(), Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging never takes the application down (a full disk, a file opened by an editor).
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void DeleteOldFiles()
    {
        var oldest = clock.GetLocalNow().Date.AddDays(-(DaysKept - 1));
        foreach (var file in Directory.EnumerateFiles(folder, "jarvis-*.log"))
        {
            var day = Path.GetFileNameWithoutExtension(file)["jarvis-".Length..];
            if (DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date < oldest)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static string ShortName(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider.minimum;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
