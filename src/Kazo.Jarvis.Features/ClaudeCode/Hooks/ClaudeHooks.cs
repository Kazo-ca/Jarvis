using System.Security.Cryptography;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.ClaudeCode.Hooks;

/// <summary>What the page of the hooks shows.</summary>
/// <param name="Available">Whether this host can reach the user's settings and a vault at all.</param>
/// <param name="State">Whether the hooks are installed.</param>
/// <param name="FilePath">The path of the user's settings file.</param>
/// <param name="FileMissing">Whether the file does not exist yet; installing creates it.</param>
/// <param name="Preview">The exact lines Jarvis adds, or <see langword="null"/> when no token could be kept.</param>
/// <param name="Port">The port the lines carry.</param>
public sealed record ClaudeHooksView(bool Available, HookInstallState State, string? FilePath, bool FileMissing, string? Preview, int Port)
{
    /// <summary>Before the first reading, and on a host without the services.</summary>
    public static ClaudeHooksView Unavailable { get; } = new(false, HookInstallState.None, null, false, null, LocalServerOptions.DefaultPort);
}

/// <summary>How an installation or a removal ended.</summary>
public enum ClaudeHooksOutcome
{
    /// <summary>The file was written.</summary>
    Done,

    /// <summary>There was nothing to remove: the file was not touched.</summary>
    Nothing,

    /// <summary>The file is not one Jarvis knows how to change: it was not touched.</summary>
    Unreadable,

    /// <summary>The file or its copy could not be written: the file is as it was.</summary>
    Failed,

    /// <summary>This host has no access to the user's settings or to a vault.</summary>
    Unavailable,
}

/// <summary>The result of an installation or a removal.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="CopyPath">The dated copy of the file as it was, when one was made.</param>
public sealed record ClaudeHooksResult(ClaudeHooksOutcome Outcome, string? CopyPath = null);

/// <summary>
/// The hooks of Jarvis in the user's own Claude Code settings (ADR 0021, section 6; US2.12). Nothing is written
/// unless <see cref="InstallAsync"/> or <see cref="RemoveAsync"/> is called, and only the page calls them, on the
/// user's click. The token of these hooks lives in the vault; it is drawn the first time the lines are shown and
/// erased when the hooks are removed, which makes every older copy of the lines useless.
/// </summary>
public sealed partial class ClaudeHooks
{
    private readonly ISettingsStore settings;
    private readonly IClaudeUserSettings? file;
    private readonly ISecretVault? vault;
    private readonly ILogger<ClaudeHooks>? logger;

    /// <summary>Creates the service.</summary>
    /// <param name="settings">The settings, for the port of the local server.</param>
    /// <param name="file">The user's settings file, a native service of the host.</param>
    /// <param name="vault">The vault, for the token.</param>
    /// <param name="logger">The log. Neither the token nor the lines are written to it.</param>
    public ClaudeHooks(ISettingsStore settings, IClaudeUserSettings? file = null, ISecretVault? vault = null, ILogger<ClaudeHooks>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.file = file;
        this.vault = vault;
        this.logger = logger;
    }

    /// <summary>Reads the state of now. Writes nothing to the user's file.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>What the page shows.</returns>
    public async ValueTask<ClaudeHooksView> ReadAsync(CancellationToken cancellationToken = default)
    {
        var port = settings.Read(LocalServerOptions.Section).Port;
        if (file is null || vault is null)
        {
            return ClaudeHooksView.Unavailable with { Port = port };
        }

        var read = await file.ReadAsync(cancellationToken).ConfigureAwait(false);
        var token = await TokenAsync(create: true, cancellationToken).ConfigureAwait(false);
        var state = read.State == ClaudeUserSettingsState.Unreadable
            ? HookInstallState.Unreadable
            : HookEntries.StateOf(read.Text, port, token ?? string.Empty);
        return new ClaudeHooksView(
            true,
            state,
            read.Path,
            read.State == ClaudeUserSettingsState.Missing,
            token is null ? null : HookEntries.Preview(port, token),
            port);
    }

    /// <summary>Adds the hooks to the user's settings, after a dated copy. Called on the user's click only.</summary>
    /// <param name="cancellationToken">Cancels before anything is written.</param>
    /// <returns>How it ended.</returns>
    public async ValueTask<ClaudeHooksResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        if (file is null || vault is null || await TokenAsync(create: true, cancellationToken).ConfigureAwait(false) is not { } token)
        {
            return new ClaudeHooksResult(ClaudeHooksOutcome.Unavailable);
        }

        var read = await file.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (read.State == ClaudeUserSettingsState.Unreadable)
        {
            return new ClaudeHooksResult(ClaudeHooksOutcome.Unreadable);
        }

        var change = HookEntries.Install(read.Text, settings.Read(LocalServerOptions.Section).Port, token);
        return await WriteAsync(change, "installed", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes the hooks of Jarvis from the user's settings, and erases their token. Called on the user's click only.</summary>
    /// <param name="cancellationToken">Cancels before anything is written.</param>
    /// <returns>How it ended.</returns>
    public async ValueTask<ClaudeHooksResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        if (file is null || vault is null)
        {
            return new ClaudeHooksResult(ClaudeHooksOutcome.Unavailable);
        }

        var read = await file.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (read.State == ClaudeUserSettingsState.Unreadable)
        {
            return new ClaudeHooksResult(ClaudeHooksOutcome.Unreadable);
        }

        var result = new ClaudeHooksResult(ClaudeHooksOutcome.Nothing);
        if (read.State == ClaudeUserSettingsState.Read)
        {
            var change = HookEntries.Remove(read.Text);
            if (change.Text is null)
            {
                return new ClaudeHooksResult(ClaudeHooksOutcome.Unreadable);
            }

            if (change.Changed > 0)
            {
                result = await WriteAsync(change, "removed", cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Outcome is ClaudeHooksOutcome.Done or ClaudeHooksOutcome.Nothing)
        {
            // Lines kept somewhere else open nothing any more.
            try
            {
                await vault.DeleteAsync(LocalServerOptions.UserHookToken, cancellationToken).ConfigureAwait(false);
            }
            catch (SecretVaultException exception)
            {
                if (logger is not null)
                {
                    LogVault(logger, exception);
                }
            }
        }

        return result;
    }

    private async ValueTask<ClaudeHooksResult> WriteAsync(HookChange change, string what, CancellationToken cancellationToken)
    {
        if (change.Text is null)
        {
            return new ClaudeHooksResult(ClaudeHooksOutcome.Unreadable);
        }

        try
        {
            var copy = await file!.WriteAsync(change.Text, cancellationToken).ConfigureAwait(false);
            if (logger is not null)
            {
                LogWritten(logger, what, change.Changed);
            }

            return new ClaudeHooksResult(ClaudeHooksOutcome.Done, copy);
        }
        catch (IOException exception)
        {
            if (logger is not null)
            {
                LogNotWritten(logger, exception);
            }

            return new ClaudeHooksResult(ClaudeHooksOutcome.Failed);
        }
    }

    private async ValueTask<string?> TokenAsync(bool create, CancellationToken cancellationToken)
    {
        try
        {
            if (await vault!.GetAsync(LocalServerOptions.UserHookToken, cancellationToken).ConfigureAwait(false) is { } kept)
            {
                return kept.Reveal();
            }

            if (!create)
            {
                return null;
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await vault.SetAsync(LocalServerOptions.UserHookToken, new SecretValue(token), cancellationToken).ConfigureAwait(false);
            return token;
        }
        catch (SecretVaultException exception)
        {
            if (logger is not null)
            {
                LogVault(logger, exception);
            }

            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hooks of Jarvis {What} in the user's Claude Code settings ({Count}), on the user's request.")]
    private static partial void LogWritten(ILogger logger, string what, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The user's Claude Code settings could not be written: they are as they were.")]
    private static partial void LogNotWritten(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The vault could not keep or give the token of the hooks.")]
    private static partial void LogVault(ILogger logger, Exception exception);
}
