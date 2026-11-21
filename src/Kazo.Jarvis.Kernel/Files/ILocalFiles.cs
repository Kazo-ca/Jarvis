namespace Kazo.Jarvis.Kernel.Files;

/// <summary>
/// The files of the PC, read only (ADR 0017, section 3): what the file tools use to resolve a path, list a folder
/// and read a text file. It decides nothing: the slice of the file tools checks the allowed folders before it
/// asks (ADR 0009, section 5). The Windows host reads the disk, the Web host a few files in memory.
/// </summary>
public interface ILocalFiles
{
    /// <summary>
    /// The full path of <paramref name="path"/> with every link and junction resolved, so that two spellings of the
    /// same place compare equal (ADR 0009, section 5). The path does not have to exist: what exists of it is
    /// resolved, the rest is kept.
    /// </summary>
    /// <param name="path">A path as a model or the user wrote it.</param>
    /// <returns>The resolved path, or <see langword="null"/> when <paramref name="path"/> is not a usable absolute path.</returns>
    string? Resolve(string path);

    /// <summary>Lists a folder: folders first, then files, by name.</summary>
    /// <param name="folder">A resolved path (<see cref="Resolve"/>).</param>
    /// <param name="most">The most entries returned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The first entries and how many the folder holds.</returns>
    /// <exception cref="LocalFileException">The folder cannot be listed.</exception>
    ValueTask<FolderListing> ListAsync(string folder, int most, CancellationToken cancellationToken = default);

    /// <summary>Reads a part of a text file.</summary>
    /// <param name="file">A resolved path (<see cref="Resolve"/>).</param>
    /// <param name="start">The first character wanted, from 0.</param>
    /// <param name="most">The most characters returned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text and how long the whole file is.</returns>
    /// <exception cref="LocalFileException">The file cannot be read as text.</exception>
    ValueTask<FileExcerpt> ReadTextAsync(string file, int start, int most, CancellationToken cancellationToken = default);
}

/// <summary>One entry of a folder.</summary>
/// <param name="Name">Its name, without the path.</param>
/// <param name="IsFolder">Whether it is a folder.</param>
/// <param name="Size">The size of a file in bytes.</param>
/// <param name="Modified">When it was last written.</param>
public sealed record FolderEntry(string Name, bool IsFolder, long? Size, DateTimeOffset? Modified);

/// <summary>What a folder holds.</summary>
/// <param name="Entries">The first entries, folders first.</param>
/// <param name="Total">How many entries the folder holds.</param>
public sealed record FolderListing(IReadOnlyList<FolderEntry> Entries, int Total);

/// <summary>A part of a text file.</summary>
/// <param name="Text">The characters read.</param>
/// <param name="Start">The position of the first one.</param>
/// <param name="TotalCharacters">The length of the whole text.</param>
public sealed record FileExcerpt(string Text, int Start, int TotalCharacters);

/// <summary>Why a folder or a file could not be read.</summary>
public enum LocalFileFailure
{
    /// <summary>Nothing exists at this path.</summary>
    NotFound,

    /// <summary>The path is a file where a folder was asked.</summary>
    NotAFolder,

    /// <summary>The path is a folder where a file was asked.</summary>
    NotAFile,

    /// <summary>Windows refused the read.</summary>
    AccessDenied,

    /// <summary>The file is not text: it holds bytes no text has.</summary>
    NotText,

    /// <summary>The file is larger than what is read in one go.</summary>
    TooLarge,

    /// <summary>The disk failed in another way.</summary>
    Unreadable,
}

/// <summary>A folder or a file that cannot be read. Its message never holds the content of a file.</summary>
public sealed class LocalFileException : Exception
{
    /// <summary>Creates the exception.</summary>
    public LocalFileException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    public LocalFileException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The cause.</param>
    public LocalFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="failure">Why.</param>
    /// <param name="innerException">The cause.</param>
    public LocalFileException(LocalFileFailure failure, Exception? innerException = null)
        : base(failure.ToString(), innerException)
    {
        Failure = failure;
    }

    /// <summary>Why.</summary>
    public LocalFileFailure Failure { get; } = LocalFileFailure.Unreadable;
}
