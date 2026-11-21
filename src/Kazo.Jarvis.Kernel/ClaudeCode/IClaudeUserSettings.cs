namespace Kazo.Jarvis.Kernel.ClaudeCode;

/// <summary>
/// The settings file of Claude Code that belongs to the user, <c>%USERPROFILE%\.claude\settings.json</c>
/// (ADR 0021, section 6). A native service of the host. Jarvis reads it to show what it would add, and writes it
/// only when the user asks, after a dated copy.
/// </summary>
public interface IClaudeUserSettings
{
    /// <summary>Reads the file.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The file, or why there is no text.</returns>
    ValueTask<ClaudeUserSettingsFile> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the file by <paramref name="text"/>, after a dated copy of the file as it was, next to it.
    /// </summary>
    /// <param name="text">The whole new content.</param>
    /// <param name="cancellationToken">Cancels the writing, before anything is replaced.</param>
    /// <returns>The path of the dated copy, or <see langword="null"/> when there was no file to copy.</returns>
    /// <exception cref="IOException">The copy or the file could not be written; the file is as it was.</exception>
    ValueTask<string?> WriteAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>What a reading of the user's settings file gave.</summary>
/// <param name="State">Whether there is a text.</param>
/// <param name="Path">The path of the file, to show.</param>
/// <param name="Text">The content, when <see cref="ClaudeUserSettingsState.Read"/>.</param>
public sealed record ClaudeUserSettingsFile(ClaudeUserSettingsState State, string Path, string? Text = null);

/// <summary>The state of the user's settings file.</summary>
public enum ClaudeUserSettingsState
{
    /// <summary>No file yet: Claude Code runs with its defaults.</summary>
    Missing,

    /// <summary>The file was read.</summary>
    Read,

    /// <summary>The file exists and could not be opened.</summary>
    Unreadable,
}
