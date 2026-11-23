using System.Diagnostics;
using System.Text;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Agents;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters.Agents;

/// <summary>
/// The agent files on the disk (ADR 0025, section 2): <c>agents\&lt;name&gt;.md</c> under the folder of Jarvis. A
/// watcher tells when a file changes; an editor that saves writes several times, so the changes of a short while
/// are told once.
/// </summary>
public sealed partial class DiskAgentFiles : IAgentFiles, IDisposable
{
    /// <summary>The largest agent file read: instructions are a page of text.</summary>
    public const long MostFileBytes = 256 * 1024;

    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(400);

    private readonly ILogger<DiskAgentFiles> logger;
    private readonly Lock gate = new();
    private readonly Timer settle;
    private FileSystemWatcher? watcher;
    private Action? changed;

    /// <summary>Creates the adapter.</summary>
    /// <param name="paths">Where Jarvis keeps its files.</param>
    /// <param name="logger">Logs a file that cannot be read, by name.</param>
    public DiskAgentFiles(JarvisPaths paths, ILogger<DiskAgentFiles>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Folder = paths.Agents;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DiskAgentFiles>.Instance;
        settle = new Timer(_ => changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed
    {
        add
        {
            lock (gate)
            {
                changed += value;
                StartWatching();
            }
        }

        remove
        {
            lock (gate)
            {
                changed -= value;
            }
        }
    }

    /// <inheritdoc />
    public string Folder { get; }

    /// <inheritdoc />
    public IReadOnlyList<AgentFile> ReadAll()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        var files = new List<AgentFile>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.md").Order(StringComparer.OrdinalIgnoreCase))
        {
            files.Add(new AgentFile(Path.GetFileNameWithoutExtension(path), Read(path)));
        }

        return files;
    }

    /// <inheritdoc />
    public void Write(string id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteAllText(PathOf(id), text);
    }

    /// <inheritdoc />
    public bool Open(string id)
    {
        try
        {
            var path = PathOf(id);
            if (!File.Exists(path))
            {
                return false;
            }

            // The editor Windows has for a .md file; the file is a document, never run.
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or ArgumentException or PlatformNotSupportedException)
        {
            LogNotOpened(exception, id);
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        watcher?.Dispose();
        settle.Dispose();
    }

    /// <summary>The path of the file of <paramref name="id"/>; an identity is a plain file name, never a path.</summary>
    private string PathOf(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("An agent identity is a plain file name.", nameof(id));
        }

        return Path.Combine(Folder, id + ".md");
    }

    private string? Read(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MostFileBytes)
            {
                return null;
            }

            // Shared with writers: a file open in an editor is read all the same.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogNotRead(exception, Path.GetFileName(path));
            return null;
        }
    }

    private void StartWatching()
    {
        if (watcher is not null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            watcher = new FileSystemWatcher(Folder, "*.md") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Without a watcher the files are read at the next start.
            LogNotWatched(exception);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => settle.Change(Settle, Timeout.InfiniteTimeSpan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent file {File} could not be read.")]
    private partial void LogNotRead(Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent file {File} could not be opened.")]
    private partial void LogNotOpened(Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The folder of the agents is not watched; a changed file is read at the next start.")]
    private partial void LogNotWatched(Exception exception);
}
