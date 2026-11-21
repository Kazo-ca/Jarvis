namespace Kazo.Jarvis.Kernel.Tools;

/// <summary>
/// The tools Jarvis knows (ADR 0017, section 1). Each slice registers its tools; the conversation reads the
/// enabled ones to give them to the model, and the decision point checks every call against it.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Raised after a tool is added or removed, or after the activation settings change; on any thread.</summary>
    event Action? Changed;

    /// <summary>Every tool known, enabled or not, by name.</summary>
    IReadOnlyList<ITool> Tools { get; }

    /// <summary>The tools given to the model: those the user enabled, or that are enabled by default (ADR 0017, section 4).</summary>
    IReadOnlyList<ITool> Enabled { get; }

    /// <summary>The tool with this exact name.</summary>
    /// <param name="name">The name the model wrote.</param>
    /// <returns>The tool, or <see langword="null"/>.</returns>
    ITool? Find(string name);

    /// <summary>Whether <paramref name="tool"/> is offered to the model.</summary>
    /// <param name="tool">A tool of the registry.</param>
    /// <returns><see langword="true"/> when its switch is on, or when the user did not choose and it is enabled by default.</returns>
    bool IsEnabled(ITool tool);

    /// <summary>Adds a tool found while Jarvis runs, for instance on an MCP server.</summary>
    /// <param name="tool">The tool. Its name must not be taken.</param>
    /// <returns>Disposing it removes the tool.</returns>
    /// <exception cref="ArgumentException">A tool already has this name.</exception>
    IDisposable Register(ITool tool);
}
