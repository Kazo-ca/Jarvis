using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Settings;

/// <summary>
/// The settings in <c>config.json</c> (ADR 0004, section 2): one section per slice, unknown sections kept,
/// atomic writes, a copy in <c>backups\</c> before a migration, and a file that cannot be read or that a newer
/// version wrote is never overwritten (the application runs on default values and <see cref="State"/> says why).
/// </summary>
public sealed class JsonFileSettingsStore : ISettingsStore
{
    private const string VersionProperty = "schemaVersion";

    private readonly string file;
    private readonly string backups;
    private readonly int currentVersion;
    private readonly TimeProvider time;
    private readonly ILogger logger;
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<Action>> watchers = new(StringComparer.Ordinal);
    private JsonObject root;

    /// <summary>Loads the settings of <paramref name="paths"/>, migrating an older file.</summary>
    /// <param name="paths">Where the files are.</param>
    /// <param name="migrations">The migrations of the slices, one per starting version.</param>
    /// <param name="time">Clock used to name the backup copies.</param>
    /// <param name="logger">Log; it never receives a setting value.</param>
    public JsonFileSettingsStore(
        JarvisPaths paths,
        IEnumerable<ISettingsMigration> migrations,
        TimeProvider time,
        ILogger<JsonFileSettingsStore>? logger = null)
        : this(paths, migrations, time, logger, SettingsSchema.CurrentVersion)
    {
    }

    /// <summary>Same, with another current version: lets the tests run a migration before the first real one exists.</summary>
    internal JsonFileSettingsStore(
        JarvisPaths paths,
        IEnumerable<ISettingsMigration> migrations,
        TimeProvider time,
        ILogger? logger,
        int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(time);

        file = paths.ConfigFile;
        backups = paths.Backups;
        this.currentVersion = currentVersion;
        this.time = time;
        this.logger = logger ?? NullLogger.Instance;
        (root, State) = Load(migrations.ToArray());
    }

    /// <inheritdoc />
    public SettingsLoadState State { get; }

    /// <inheritdoc />
    public T Read<T>(SettingsSection<T> section)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(section);
        JsonNode? node;
        lock (gate)
        {
            node = root[section.Name]?.DeepClone();
        }

        if (node is null)
        {
            return section.CreateDefault();
        }

        T? value;
        try
        {
            value = node.Deserialize<T>(JarvisJson.Options);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            logger.LogWarning("Settings section {Section} cannot be read; its default values are used.", section.Name);
            return section.CreateDefault();
        }

        if (value is null || section.Validate(value).Count > 0)
        {
            logger.LogWarning("Settings section {Section} is not valid; its default values are used.", section.Name);
            return section.CreateDefault();
        }

        return value;
    }

    /// <inheritdoc />
    public ValueTask<SettingsWriteResult> WriteAsync<T>(SettingsSection<T> section, T value, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var problems = section.Validate(value);
        if (problems.Count > 0)
        {
            return ValueTask.FromResult(SettingsWriteResult.Invalid(problems));
        }

        if (!State.CanSave)
        {
            logger.LogWarning("Settings section {Section} not saved: the settings file is {Outcome}.", section.Name, State.Outcome);
            return ValueTask.FromResult(SettingsWriteResult.ReadOnly);
        }

        Action[] toNotify;
        lock (gate)
        {
            var previous = root[section.Name]?.DeepClone();
            root[section.Name] = JsonSerializer.SerializeToNode(value, JarvisJson.Options);
            try
            {
                AtomicFile.WriteAllText(file, root.ToJsonString(JarvisJson.Options));
            }
            catch
            {
                root[section.Name] = previous;
                throw;
            }

            toNotify = watchers.TryGetValue(section.Name, out var list) ? [.. list] : [];
        }

        foreach (var notify in toNotify)
        {
            try
            {
                notify();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "A watcher of settings section {Section} failed.", section.Name);
            }
        }

        return ValueTask.FromResult(SettingsWriteResult.Saved);
    }

    /// <inheritdoc />
    public IDisposable Watch<T>(SettingsSection<T> section, Action<T> handler)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(handler);

        Action notify = () => handler(Read(section));
        lock (gate)
        {
            if (!watchers.TryGetValue(section.Name, out var list))
            {
                watchers[section.Name] = list = [];
            }

            list.Add(notify);
        }

        return new Subscription(() =>
        {
            lock (gate)
            {
                watchers[section.Name].Remove(notify);
            }
        });
    }

    private (JsonObject Root, SettingsLoadState State) Load(ISettingsMigration[] migrations)
    {
        if (!File.Exists(file))
        {
            logger.LogInformation("No settings file: first run.");
            return (Empty(), new SettingsLoadState(SettingsLoadOutcome.Missing));
        }

        JsonObject? content;
        try
        {
            content = JsonNode.Parse(File.ReadAllText(file), documentOptions: JarvisJson.DocumentOptions) as JsonObject;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            content = null;
        }

        if (content is null || ReadVersion(content) is not { } version)
        {
            TryBackup("unreadable");
            logger.LogError("The settings file cannot be read. It is left untouched; default values are used and nothing is saved.");
            return (Empty(), new SettingsLoadState(SettingsLoadOutcome.Unreadable));
        }

        if (version > currentVersion)
        {
            logger.LogError(
                "The settings file was written by a newer version (schema {FileVersion}, this one knows {CurrentVersion}). It is left untouched.",
                version, currentVersion);
            return (Empty(), new SettingsLoadState(SettingsLoadOutcome.NewerVersion, version));
        }

        if (version == currentVersion)
        {
            return (content, new SettingsLoadState(SettingsLoadOutcome.Loaded, version));
        }

        if (!TryBackup($"v{version}"))
        {
            return (Empty(), new SettingsLoadState(SettingsLoadOutcome.MigrationFailed, version));
        }

        try
        {
            for (var from = version; from < currentVersion; from++)
            {
                var step = migrations.SingleOrDefault(migration => migration.FromVersion == from)
                    ?? throw new InvalidOperationException($"No settings migration from version {from}.");
                step.Migrate(content);
                content[VersionProperty] = from + 1;
            }

            AtomicFile.WriteAllText(file, content.ToJsonString(JarvisJson.Options));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Migrating the settings file from schema {FileVersion} failed. It is left untouched.", version);
            return (Empty(), new SettingsLoadState(SettingsLoadOutcome.MigrationFailed, version));
        }

        logger.LogInformation("Settings file migrated from schema {FileVersion} to {CurrentVersion}.", version, currentVersion);
        return (content, new SettingsLoadState(SettingsLoadOutcome.Migrated, version));
    }

    private JsonObject Empty() => new() { [VersionProperty] = currentVersion };

    private static int? ReadVersion(JsonObject content) =>
        content[VersionProperty] is JsonValue value && value.TryGetValue<int>(out var version) && version >= 1 ? version : null;

    private bool TryBackup(string reason)
    {
        try
        {
            AtomicFile.Backup(file, backups, reason, time.GetUtcNow());
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "The settings file could not be copied to the backups folder.");
            return false;
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
