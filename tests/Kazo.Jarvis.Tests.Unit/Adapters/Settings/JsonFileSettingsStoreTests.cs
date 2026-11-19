using System.Text.Json.Nodes;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Settings;

/// <summary>The settings file, config.json (ADR 0004, section 2; stage 1 plan, work item 3).</summary>
[TestFixture]
public sealed class JsonFileSettingsStoreTests
{
    private static readonly SettingsSection<SampleOptions> Sample = new(
        "Test.Sample",
        () => new SampleOptions(),
        value => value.Volume is < 0 or > 100 ? ["The volume is between 0 and 100."] : []);

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 20, 30, 0, TimeSpan.Zero));
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    private string ConfigFile => folder.Paths.ConfigFile;

    [Test]
    public void Without_a_file_it_is_a_first_run_on_default_values_and_nothing_is_written()
    {
        var store = Open();

        Assert.Multiple(() =>
        {
            Assert.That(store.State.Outcome, Is.EqualTo(SettingsLoadOutcome.Missing));
            Assert.That(store.State.IsFirstRun, Is.True);
            Assert.That(store.State.CanSave, Is.True);
            Assert.That(store.Read(Sample), Is.EqualTo(new SampleOptions()));
            Assert.That(File.Exists(ConfigFile), Is.False);
        });
    }

    [Test]
    public async Task A_saved_section_is_read_back_after_a_restart()
    {
        var result = await Open().WriteAsync(Sample, new SampleOptions { Volume = 30, Name = "Jarvis" });

        var reopened = Open();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(reopened.State.Outcome, Is.EqualTo(SettingsLoadOutcome.Loaded));
            Assert.That(reopened.Read(Sample), Is.EqualTo(new SampleOptions { Volume = 30, Name = "Jarvis" }));
            Assert.That(ReadFile()["schemaVersion"]!.GetValue<int>(), Is.EqualTo(SettingsSchema.CurrentVersion));
            Assert.That(ReadFile()["Test.Sample"]!["volume"]!.GetValue<int>(), Is.EqualTo(30));
        });
    }

    [Test]
    public async Task An_unknown_section_is_kept_on_save()
    {
        WriteFile("""{ "schemaVersion": 1, "Removed.Slice": { "keep": [1, 2, 3] } }""");

        await Open().WriteAsync(Sample, new SampleOptions { Volume = 10 });

        Assert.That(ReadFile()["Removed.Slice"]!["keep"]!.AsArray().Select(node => node!.GetValue<int>()), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task An_invalid_value_is_refused_and_not_written()
    {
        var store = Open();

        var result = await store.WriteAsync(Sample, new SampleOptions { Volume = 150 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(result.Problems, Is.EqualTo(new[] { "The volume is between 0 and 100." }));
            Assert.That(File.Exists(ConfigFile), Is.False);
        });
    }

    [TestCase("this is not JSON")]
    [TestCase("""{ "Test.Sample": { "volume": 5 } }""")]
    [TestCase("""{ "schemaVersion": "one" }""")]
    [TestCase("""[1, 2]""")]
    public async Task An_unreadable_file_is_copied_never_overwritten_and_defaults_are_used(string content)
    {
        WriteFile(content);
        var store = Open();

        var result = await store.WriteAsync(Sample, new SampleOptions { Volume = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(store.State.Outcome, Is.EqualTo(SettingsLoadOutcome.Unreadable));
            Assert.That(store.State.CanSave, Is.False);
            Assert.That(store.Read(Sample), Is.EqualTo(new SampleOptions()));
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.ReadOnly));
            Assert.That(File.ReadAllText(ConfigFile), Is.EqualTo(content));
            Assert.That(Backups(), Has.Exactly(1).Matches<string>(name => name == "config.unreadable.20261119-203000.json"));
        });
    }

    [Test]
    public async Task A_file_from_a_newer_version_is_never_overwritten()
    {
        const string content = """{ "schemaVersion": 2, "Test.Sample": { "volume": 5 } }""";
        WriteFile(content);
        var store = Open();

        var result = await store.WriteAsync(Sample, new SampleOptions { Volume = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(store.State, Is.EqualTo(new SettingsLoadState(SettingsLoadOutcome.NewerVersion, 2)));
            Assert.That(store.Read(Sample), Is.EqualTo(new SampleOptions()));
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.ReadOnly));
            Assert.That(File.ReadAllText(ConfigFile), Is.EqualTo(content));
        });
    }

    [Test]
    public void An_older_file_is_copied_to_backups_then_migrated()
    {
        const string content = """{ "schemaVersion": 1, "Test.Sample": { "level": 42 }, "Other.Slice": { "x": 1 } }""";
        WriteFile(content);

        var store = Open(new RenameLevelToVolume(), currentVersion: 2);

        Assert.Multiple(() =>
        {
            Assert.That(store.State, Is.EqualTo(new SettingsLoadState(SettingsLoadOutcome.Migrated, 1)));
            Assert.That(store.Read(Sample).Volume, Is.EqualTo(42));
            Assert.That(ReadFile()["schemaVersion"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(ReadFile()["Other.Slice"]!["x"]!.GetValue<int>(), Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(folder.Paths.Backups, "config.v1.20261119-203000.json")), Is.EqualTo(content));
        });
    }

    [Test]
    public async Task A_missing_migration_leaves_the_file_untouched()
    {
        const string content = """{ "schemaVersion": 1, "Test.Sample": { "volume": 42 } }""";
        WriteFile(content);
        var store = Open(migration: null, currentVersion: 2);

        var result = await store.WriteAsync(Sample, new SampleOptions());

        Assert.Multiple(() =>
        {
            Assert.That(store.State.Outcome, Is.EqualTo(SettingsLoadOutcome.MigrationFailed));
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.ReadOnly));
            Assert.That(store.Read(Sample), Is.EqualTo(new SampleOptions()));
            Assert.That(File.ReadAllText(ConfigFile), Is.EqualTo(content));
        });
    }

    [TestCase("""{ "schemaVersion": 1, "Test.Sample": { "volume": 400 } }""")]
    [TestCase("""{ "schemaVersion": 1, "Test.Sample": { "volume": "loud" } }""")]
    [TestCase("""{ "schemaVersion": 1, "Test.Sample": 12 }""")]
    public void An_invalid_section_gives_its_defaults_and_is_logged(string content)
    {
        WriteFile(content);
        var log = new ListLogger();

        var value = Open(logger: log).Read(Sample);

        Assert.Multiple(() =>
        {
            Assert.That(value, Is.EqualTo(new SampleOptions()));
            Assert.That(log.Entries, Has.Some.Matches<(Microsoft.Extensions.Logging.LogLevel Level, string Message, Exception? Exception)>(
                entry => entry.Message.Contains("Test.Sample", StringComparison.Ordinal)));
        });
    }

    [Test]
    public async Task Watchers_receive_the_new_value_until_they_unsubscribe()
    {
        var store = Open();
        var received = new List<int>();
        var subscription = store.Watch(Sample, value => received.Add(value.Volume));

        await store.WriteAsync(Sample, new SampleOptions { Volume = 20 });
        subscription.Dispose();
        await store.WriteAsync(Sample, new SampleOptions { Volume = 30 });

        Assert.That(received, Is.EqualTo(new[] { 20 }));
    }

    [Test]
    public async Task A_failing_watcher_does_not_cancel_the_save()
    {
        var store = Open(logger: new ListLogger());
        using var failing = store.Watch(Sample, _ => throw new InvalidOperationException("boom"));
        var received = new List<int>();
        using var other = store.Watch(Sample, value => received.Add(value.Volume));

        var result = await store.WriteAsync(Sample, new SampleOptions { Volume = 20 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(received, Is.EqualTo(new[] { 20 }));
        });
    }

    [Test]
    public async Task A_save_leaves_no_temporary_file()
    {
        var store = Open();

        await store.WriteAsync(Sample, new SampleOptions { Volume = 1 });
        await store.WriteAsync(Sample, new SampleOptions { Volume = 2 });

        Assert.That(Directory.GetFiles(folder.Paths.Root).Select(Path.GetFileName), Is.EqualTo(new[] { "config.json" }));
    }

    [Test]
    public void A_read_returns_a_copy()
    {
        var store = Open();
        var list = new SettingsSection<List<int>>("Test.List", () => [1]);

        store.Read(list).Add(2);

        Assert.That(store.Read(list), Is.EqualTo(new[] { 1 }));
    }

    private JsonFileSettingsStore Open(ISettingsMigration? migration = null, int currentVersion = SettingsSchema.CurrentVersion, ListLogger? logger = null) =>
        new(folder.Paths, migration is null ? [] : [migration], time, logger, currentVersion);

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(folder.Paths.Root);
        File.WriteAllText(ConfigFile, content);
    }

    private JsonObject ReadFile() => JsonNode.Parse(File.ReadAllText(ConfigFile))!.AsObject();

    private string[] Backups() =>
        Directory.Exists(folder.Paths.Backups) ? Directory.GetFiles(folder.Paths.Backups).Select(file => Path.GetFileName(file)!).ToArray() : [];

    private sealed record SampleOptions
    {
        public int Volume { get; init; } = 50;

        public string Name { get; init; } = "default";
    }

    /// <summary>A version 1 → 2 migration: <c>level</c> becomes <c>volume</c>.</summary>
    private sealed class RenameLevelToVolume : ISettingsMigration
    {
        public int FromVersion => 1;

        public void Migrate(JsonObject root)
        {
            var section = root["Test.Sample"]!.AsObject();
            var level = section["level"]!.GetValue<int>();
            section.Remove("level");
            section["volume"] = level;
        }
    }
}
