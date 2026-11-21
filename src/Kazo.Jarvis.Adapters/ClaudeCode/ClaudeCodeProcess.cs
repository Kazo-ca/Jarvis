using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.LocalServer;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters.ClaudeCode;

/// <summary>
/// Starts Claude Code as a process (ADR 0021, section 1). The files of Jarvis live under
/// <c>%USERPROFILE%\.kazo\jarvis\claude\</c> and are rewritten at each launch; nothing is ever written under
/// <c>%USERPROFILE%\.claude\</c>. The token of the run reaches the session by its environment only.
/// </summary>
public sealed partial class ClaudeCodeProcess : IClaudeCode
{
    private const int MostError = 300;
    private static readonly TimeSpan VersionWait = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string folder;
    private readonly ILocalServer server;
    private readonly ILogger<ClaudeCodeProcess>? logger;
    private readonly string? executable;

    /// <summary>Creates the launcher.</summary>
    /// <param name="paths">The folders of Jarvis.</param>
    /// <param name="server">The local server the session calls back.</param>
    /// <param name="logger">The log. Neither the token nor a message of a session is written to it.</param>
    public ClaudeCodeProcess(JarvisPaths paths, ILocalServer server, ILogger<ClaudeCodeProcess>? logger = null)
        : this(paths, server, logger, null)
    {
    }

    internal ClaudeCodeProcess(JarvisPaths paths, ILocalServer server, ILogger<ClaudeCodeProcess>? logger, string? executable)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(server);
        folder = Path.Combine(paths.Root, "claude");
        this.server = server;
        this.logger = logger;
        this.executable = executable;
    }

    internal string SettingsFile => Path.Combine(folder, "settings.json");

    internal string McpFile => Path.Combine(folder, "mcp.json");

    /// <inheritdoc />
    public async ValueTask<ClaudeCodeInstall> FindAsync(CancellationToken cancellationToken = default)
    {
        if (Locate() is not { } path)
        {
            return new ClaudeCodeInstall(false);
        }

        try
        {
            var start = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("--version");
            using var process = Process.Start(start);
            if (process is null)
            {
                return new ClaudeCodeInstall(false);
            }

            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(VersionWait);
            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(bounded.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
                return new ClaudeCodeInstall(true, VersionPattern().Match(output) is { Success: true } match ? Version.Parse(match.Value) : null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                return new ClaudeCodeInstall(true);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return new ClaudeCodeInstall(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IClaudeRun> StartAsync(ClaudeLaunch launch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (Locate() is not { } path)
        {
            throw new ClaudeCodeException("Claude Code was not found on this computer.");
        }

        var status = server.Status;
        if (!status.Listening)
        {
            // Without the server the session could not ask its permissions: it is not started.
            throw new ClaudeCodeException("The local server of Jarvis does not listen.");
        }

        if (!Directory.Exists(launch.Folder))
        {
            throw new ClaudeCodeException("The folder of the task does not exist.");
        }

        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(SettingsFile, ClaudeCommandLine.Settings(status.Port), Utf8, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(McpFile, ClaudeCommandLine.Mcp(status.Port, KestrelLocalServer.McpPath), Utf8, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ClaudeCodeException("The files of Jarvis for Claude Code could not be written.", exception);
        }

        var instructions = Path.Combine(launch.Folder, "CLAUDE.md");
        var start = new ProcessStartInfo(path)
        {
            WorkingDirectory = launch.Folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in ClaudeCommandLine.Arguments(launch, SettingsFile, McpFile, File.Exists(instructions) ? instructions : null))
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment[ClaudeCommandLine.TaskVariable] = launch.TaskId;
        start.Environment[ClaudeCommandLine.TokenVariable] = server.SessionToken;

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new ClaudeCodeException("Claude Code did not start.");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ClaudeCodeException("Claude Code did not start.", exception);
        }

        if (logger is not null)
        {
            LogStarted(logger, launch.TaskId, process.Id);
        }

        return new Run(process, launch.FirstMessage, launch.TaskId, logger);
    }

    private string? Locate()
    {
        if (executable is not null)
        {
            return File.Exists(executable) ? executable : null;
        }

        // The program itself, not a script: a .cmd would need a shell between Jarvis and the session.
        var name = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    private static partial Regex VersionPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Code started for the task {Task} (process {Process}).")]
    private static partial void LogStarted(ILogger logger, string task, int process);

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Code ended for the task {Task} with the code {Code}.")]
    private static partial void LogEnded(ILogger logger, string task, int code);

    private sealed class Run : IClaudeRun
    {
        private readonly Process process;
        private readonly Channel<ClaudeRunEvent> events = Channel.CreateUnbounded<ClaudeRunEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        private readonly Task pump;
        private int disposed;

        public Run(Process process, string message, string task, ILogger? logger)
        {
            this.process = process;
            pump = Task.Run(() => PumpAsync(message, task, logger));
        }

        public async IAsyncEnumerable<ClaudeRunEvent> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var happened in events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return happened;
            }
        }

        public async ValueTask StopAsync()
        {
            Kill(process);
            await pump.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                await StopAsync().ConfigureAwait(false);
                process.Dispose();
            }
        }

        private async Task PumpAsync(string message, string task, ILogger? logger)
        {
            var code = -1;
            string? error = null;
            try
            {
                var errors = process.StandardError.ReadToEndAsync();
                try
                {
                    // One message, then the input is closed: the session ends by itself once its work is over.
                    await process.StandardInput.WriteLineAsync(ClaudeStream.UserMessage(message)).ConfigureAwait(false);
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    // The process is already gone: its exit code tells.
                }

                while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    foreach (var happened in ClaudeStream.Read(line))
                    {
                        events.Writer.TryWrite(happened);
                    }
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
                code = process.ExitCode;
                var written = (await errors.ConfigureAwait(false)).Trim();
                error = written.Length == 0 ? null : written.Length <= MostError ? written : written[^MostError..];
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                error = exception.Message;
            }
            finally
            {
                if (logger is not null)
                {
                    LogEnded(logger, task, code);
                }

                events.Writer.TryWrite(new ClaudeExited(code, error));
                events.Writer.TryComplete();
            }
        }
    }
}
