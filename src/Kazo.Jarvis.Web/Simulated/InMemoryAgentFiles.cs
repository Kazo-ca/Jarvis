using Kazo.Jarvis.Kernel.Agents;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IAgentFiles"/> (ADR 0025, section 2): the agent files in memory. The tests write, change and
/// remove a file as the user would in an editor, and see which file the page asked to open.
/// </summary>
public sealed class InMemoryAgentFiles : IAgentFiles
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, string?> files = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string Folder => "/home/demo/.kazo/jarvis/agents";

    /// <summary>The last file the page asked to open.</summary>
    public string? Opened { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<AgentFile> ReadAll()
    {
        lock (gate)
        {
            return [.. files.OrderBy(file => file.Key, StringComparer.OrdinalIgnoreCase).Select(file => new AgentFile(file.Key, file.Value))];
        }
    }

    /// <inheritdoc />
    public void Write(string id, string text)
    {
        lock (gate)
        {
            files[id] = text;
        }
    }

    /// <inheritdoc />
    public bool Open(string id)
    {
        lock (gate)
        {
            if (!files.ContainsKey(id))
            {
                return false;
            }
        }

        Opened = id;
        return true;
    }

    /// <summary>The user saves a file in an editor: its whole text, or <see langword="null"/> for a file the disk refuses to read.</summary>
    /// <param name="id">The name of the file without its extension.</param>
    /// <param name="text">Its text.</param>
    public void Save(string id, string? text)
    {
        lock (gate)
        {
            files[id] = text;
        }

        Changed?.Invoke();
    }

    /// <summary>The user deletes a file.</summary>
    /// <param name="id">The name of the file without its extension.</param>
    public void Delete(string id)
    {
        lock (gate)
        {
            files.Remove(id);
        }

        Changed?.Invoke();
    }
}
