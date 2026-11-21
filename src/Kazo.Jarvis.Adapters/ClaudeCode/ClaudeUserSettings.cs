using System.Globalization;
using System.Text;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Adapters.ClaudeCode;

/// <summary>
/// The user's Claude Code settings file on the disk (ADR 0021, section 6). Nothing here decides to write: the
/// slice calls <see cref="WriteAsync"/> on the user's click only. A write first copies the file as it was to
/// <c>settings.json.jarvis-&lt;date&gt;.bak</c>, then replaces it in one move, so that a failure leaves the file whole.
/// </summary>
public sealed class ClaudeUserSettings : IClaudeUserSettings
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string path;
    private readonly TimeProvider time;

    /// <summary>Creates the reader of the current user's file.</summary>
    public ClaudeUserSettings()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), TimeProvider.System)
    {
    }

    internal ClaudeUserSettings(string claudeHome, TimeProvider time)
    {
        path = Path.Combine(claudeHome, "settings.json");
        this.time = time;
    }

    /// <inheritdoc />
    public async ValueTask<ClaudeUserSettingsFile> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new ClaudeUserSettingsFile(ClaudeUserSettingsState.Missing, path);
        }

        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return new ClaudeUserSettingsFile(ClaudeUserSettingsState.Read, path, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ClaudeUserSettingsFile(ClaudeUserSettingsState.Unreadable, path);
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> WriteAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        var temporary = path + ".jarvis-new";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string? copy = null;
            if (File.Exists(path))
            {
                copy = FreeCopyPath();
                File.Copy(path, copy);
            }

            // Not cancellable from here: a half-written file would be worse than a late one.
            await File.WriteAllTextAsync(temporary, text, Utf8, CancellationToken.None).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            return copy;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException(exception.Message, exception);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The leftover holds nothing the settings file does not.
            }
        }
    }

    private string FreeCopyPath()
    {
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var copy = $"{path}.jarvis-{stamp}.bak";
        for (var again = 2; File.Exists(copy); again++)
        {
            copy = $"{path}.jarvis-{stamp}-{again}.bak";
        }

        return copy;
    }
}
