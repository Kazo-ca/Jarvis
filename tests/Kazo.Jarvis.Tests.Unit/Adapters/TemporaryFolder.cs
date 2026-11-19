using Kazo.Jarvis.Adapters.Files;

namespace Kazo.Jarvis.Tests.Unit.Adapters;

/// <summary>A folder of its own for one test, deleted afterwards: the adapters never touch the real profile.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    public TemporaryFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kazo-jarvis-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Paths = new JarvisPaths(System.IO.Path.Combine(Path, ".kazo", "jarvis"));
    }

    public string Path { get; }

    /// <summary>A Jarvis root inside the folder, not created yet.</summary>
    public JarvisPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A file still open on Windows: the temporary folder is cleaned by the system later.
        }
    }
}
