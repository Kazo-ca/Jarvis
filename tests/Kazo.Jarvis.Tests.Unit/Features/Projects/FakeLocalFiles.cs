using Kazo.Jarvis.Kernel.Files;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>Text files kept in memory; a path is usable when it is rooted at a drive or at « / ».</summary>
internal sealed class FakeLocalFiles : ILocalFiles
{
    public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Resolve(string path) =>
        !string.IsNullOrWhiteSpace(path) && (path.StartsWith('/') || (path.Length > 2 && path[1] == ':')) ? path : null;

    public ValueTask<FolderListing> ListAsync(string folder, int most, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new FolderListing([], 0));

    public ValueTask<FileExcerpt> ReadTextAsync(string file, int start, int most, CancellationToken cancellationToken = default) =>
        Texts.TryGetValue(file, out var text)
            ? ValueTask.FromResult(new FileExcerpt(text[Math.Min(start, text.Length)..Math.Min(text.Length, start + most)], start, text.Length))
            : throw new LocalFileException(LocalFileFailure.NotFound);

    /// <summary>Makes every write fail, as a folder the user may not write in.</summary>
    public bool ReadOnly { get; set; }

    public ValueTask AppendTextAsync(string file, string text, CancellationToken cancellationToken = default)
    {
        if (ReadOnly)
        {
            throw new LocalFileException(LocalFileFailure.AccessDenied);
        }

        Texts[file] = (Texts.TryGetValue(file, out var known) ? known : string.Empty) + text;
        return ValueTask.CompletedTask;
    }
}
