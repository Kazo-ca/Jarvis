using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated settings for the Web host (ADR 0004, section 2): same contract as <c>config.json</c>, kept in
/// memory, so every page load of a test starts on default values. Values are stored as JSON, like in the file,
/// so a slice that only works with the real store fails here too.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock gate = new();
    private readonly Dictionary<string, JsonNode> sections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action>> watchers = new(StringComparer.Ordinal);

    /// <summary>Creates an empty store, as on a first run.</summary>
    /// <param name="state">The load state to simulate; a first run by default.</param>
    public InMemorySettingsStore(SettingsLoadState? state = null) => State = state ?? new SettingsLoadState(SettingsLoadOutcome.Missing);

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
            node = sections.GetValueOrDefault(section.Name)?.DeepClone();
        }

        return node?.Deserialize<T>(Options) ?? section.CreateDefault();
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
            return ValueTask.FromResult(SettingsWriteResult.ReadOnly);
        }

        Action[] toNotify;
        lock (gate)
        {
            sections[section.Name] = JsonSerializer.SerializeToNode(value, Options)!;
            toNotify = watchers.TryGetValue(section.Name, out var list) ? [.. list] : [];
        }

        foreach (var notify in toNotify)
        {
            notify();
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
