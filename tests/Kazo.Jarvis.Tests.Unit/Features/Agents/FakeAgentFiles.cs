using Kazo.Jarvis.Kernel.Agents;

namespace Kazo.Jarvis.Tests.Unit.Features.Agents;

/// <summary>The folder of the agent files, in memory: a test saves and deletes a file as the user would.</summary>
internal sealed class FakeAgentFiles : IAgentFiles
{
    private readonly Dictionary<string, string?> files = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public string Folder => "/agents";

    /// <summary>The files Jarvis wrote, in order.</summary>
    public List<string> Written { get; } = [];

    public List<string> Opened { get; } = [];

    /// <summary>When set, writing fails as on a disk that refuses.</summary>
    public bool RefuseWrites { get; set; }

    public string? this[string id] => files.GetValueOrDefault(id);

    public IReadOnlyList<AgentFile> ReadAll() => [.. files.Select(file => new AgentFile(file.Key, file.Value))];

    public void Write(string id, string text)
    {
        if (RefuseWrites)
        {
            throw new IOException("The disk refuses.");
        }

        files[id] = text;
        Written.Add(id);
    }

    public bool Open(string id)
    {
        Opened.Add(id);
        return files.ContainsKey(id);
    }

    public void Save(string id, string? text)
    {
        files[id] = text;
        Changed?.Invoke();
    }

    public void Delete(string id)
    {
        files.Remove(id);
        Changed?.Invoke();
    }
}
