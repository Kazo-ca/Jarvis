using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Kernel.Tools;

/// <summary>
/// The registry of the tools, in memory: the tools the slices registered in the services, then those added while
/// Jarvis runs. It holds no classification: that is the tool's and the decision point's.
/// </summary>
public sealed class ToolRegistry : IToolRegistry, IDisposable
{
    private readonly ISettingsStore settings;
    private readonly Lock gate = new();
    private readonly Dictionary<string, ITool> tools = new(StringComparer.Ordinal);
    private readonly IDisposable watch;

    /// <summary>Creates the registry.</summary>
    /// <param name="builtIn">The tools registered by the slices.</param>
    /// <param name="settings">The settings, for <see cref="ToolActivation.Section"/>.</param>
    public ToolRegistry(IEnumerable<ITool> builtIn, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(builtIn);
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        foreach (var tool in builtIn)
        {
            Add(tool);
        }

        watch = settings.Watch(ToolActivation.Section, _ => Changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<ITool> Tools
    {
        get
        {
            lock (gate)
            {
                return [.. tools.Values.OrderBy(tool => tool.Name, StringComparer.Ordinal)];
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ITool> Enabled
    {
        get
        {
            var switches = settings.Read(ToolActivation.Section).Switches;
            return [.. Tools.Where(tool => IsEnabled(tool, switches))];
        }
    }

    /// <inheritdoc />
    public ITool? Find(string name)
    {
        if (name is null)
        {
            return null;
        }

        lock (gate)
        {
            return tools.GetValueOrDefault(name);
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return IsEnabled(tool, settings.Read(ToolActivation.Section).Switches);
    }

    /// <inheritdoc />
    public IDisposable Register(ITool tool)
    {
        Add(tool);
        Changed?.Invoke();
        return new Registration(this, tool);
    }

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();

    private static bool IsEnabled(ITool tool, IReadOnlyDictionary<string, bool>? switches) =>
        switches is not null && switches.TryGetValue(tool.Name, out var chosen) ? chosen : tool.EnabledByDefault;

    private void Add(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        lock (gate)
        {
            if (!tools.TryAdd(tool.Name, tool))
            {
                throw new ArgumentException($"A tool is already named '{tool.Name}'.", nameof(tool));
            }
        }
    }

    private void Remove(ITool tool)
    {
        bool removed;
        lock (gate)
        {
            removed = tools.TryGetValue(tool.Name, out var existing) && ReferenceEquals(existing, tool) && tools.Remove(tool.Name);
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private sealed class Registration(ToolRegistry owner, ITool tool) : IDisposable
    {
        public void Dispose() => owner.Remove(tool);
    }
}
