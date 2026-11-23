namespace Kazo.Jarvis.Kernel.Agents;

/// <summary>
/// The folder of the agent files (ADR 0025, section 2): <c>agents\&lt;name&gt;.md</c> in the folder of Jarvis. The
/// Windows host reads the disk, the Web host keeps a few files in memory. It decides nothing: the agents slice
/// reads the files, writes the starting ones and never overwrites a file the user changed.
/// </summary>
public interface IAgentFiles
{
    /// <summary>Raised when a file of the folder was added, changed or removed; on any thread.</summary>
    event Action? Changed;

    /// <summary>The folder, as shown to the user.</summary>
    string Folder { get; }

    /// <summary>Every <c>.md</c> file of the folder.</summary>
    /// <returns>The files by name; a file that cannot be read has no text.</returns>
    IReadOnlyList<AgentFile> ReadAll();

    /// <summary>Writes a file, creating the folder when it is missing.</summary>
    /// <param name="id">The name of the file without its extension.</param>
    /// <param name="text">Its whole text.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    void Write(string id, string text);

    /// <summary>Opens a file in the editor the user has for it.</summary>
    /// <param name="id">The name of the file without its extension.</param>
    /// <returns>Whether something opened.</returns>
    bool Open(string id);
}

/// <summary>One file of the folder of the agents.</summary>
/// <param name="Id">Its name without the extension.</param>
/// <param name="Text">Its text, or <see langword="null"/> when it could not be read.</param>
public sealed record AgentFile(string Id, string? Text);
