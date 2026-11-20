using Kazo.Jarvis.Adapters.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Logging;

/// <summary>The technical log of the Windows host: one file per day, kept 14 days.</summary>
[TestFixture]
public sealed class FileLoggerProviderTests
{
    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 21, 30, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    private string Logs => folder.Paths.Logs;

    [Test]
    public void Lines_go_to_the_file_of_the_day_with_level_category_and_exception()
    {
        using var provider = new FileLoggerProvider(Logs, time);
        var logger = provider.CreateLogger("Kazo.Jarvis.Windows.JarvisApp");

        logger.LogWarning("Session locked: waiting.");
        logger.LogError(new InvalidOperationException("boom"), "It failed.");

        var text = File.ReadAllText(Path.Combine(Logs, "jarvis-2026-11-19.log"));
        Assert.Multiple(() =>
        {
            Assert.That(provider.CurrentFile, Is.EqualTo(Path.Combine(Logs, "jarvis-2026-11-19.log")));
            Assert.That(text, Does.Contain("2026-11-19 21:30:00.000 +00:00 [WRN] Kazo.Jarvis.Windows.JarvisApp: Session locked: waiting."));
            Assert.That(text, Does.Contain("[ERR] Kazo.Jarvis.Windows.JarvisApp: It failed."));
            Assert.That(text, Does.Contain("System.InvalidOperationException: boom"));
        });
    }

    [Test]
    public void Lines_below_the_minimum_level_are_dropped()
    {
        using var provider = new FileLoggerProvider(Logs, time, LogLevel.Information);
        var logger = provider.CreateLogger("Test");

        logger.LogDebug("hidden");
        logger.LogInformation("shown");

        var text = File.ReadAllText(provider.CurrentFile);
        Assert.Multiple(() =>
        {
            Assert.That(logger.IsEnabled(LogLevel.Debug), Is.False);
            Assert.That(text, Does.Not.Contain("hidden"));
            Assert.That(text, Does.Contain("shown"));
        });
    }

    [Test]
    public void A_new_day_starts_a_new_file()
    {
        using var provider = new FileLoggerProvider(Logs, time);
        var logger = provider.CreateLogger("Test");

        logger.LogInformation("first day");
        time.Advance(TimeSpan.FromHours(3));
        logger.LogInformation("second day");

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(Logs, "jarvis-2026-11-19.log")), Does.Contain("first day").And.Not.Contain("second day"));
            Assert.That(File.ReadAllText(Path.Combine(Logs, "jarvis-2026-11-20.log")), Does.Contain("second day"));
        });
    }

    [Test]
    public void Files_older_than_fourteen_days_are_deleted_at_start()
    {
        Directory.CreateDirectory(Logs);
        File.WriteAllText(Path.Combine(Logs, "jarvis-2026-11-06.log"), "kept");
        File.WriteAllText(Path.Combine(Logs, "jarvis-2026-11-05.log"), "deleted");
        File.WriteAllText(Path.Combine(Logs, "notes.txt"), "not a log");

        using var provider = new FileLoggerProvider(Logs, time);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(Logs, "jarvis-2026-11-06.log")), Is.True);
            Assert.That(File.Exists(Path.Combine(Logs, "jarvis-2026-11-05.log")), Is.False);
            Assert.That(File.Exists(Path.Combine(Logs, "notes.txt")), Is.True);
        });
    }
}
