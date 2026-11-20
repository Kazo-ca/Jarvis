using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Updates;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>
/// The updates slice (US1.18): compares the running version with the releases published on GitHub,
/// downloads the newer one while the application keeps running, then hands it over to the updater.
/// One operation at a time; callable from any thread.
/// </summary>
public sealed partial class UpdateService
{
    private readonly IReleaseSource releases;
    private readonly IUpdateLauncher launcher;
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly ILogger<UpdateService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Lock statusLock = new();
    private UpdateStatus status = UpdateStatus.Initial;
    private string? announced;

    /// <summary>Creates the service.</summary>
    /// <param name="releases">Where releases are published.</param>
    /// <param name="launcher">Starts the updater; knows the running version.</param>
    /// <param name="settings">The settings, for <see cref="UpdateOptions.Section"/>.</param>
    /// <param name="bus">Publishes <see cref="UpdateFound"/>.</param>
    /// <param name="logger">Logs checks and failures.</param>
    public UpdateService(IReleaseSource releases, IUpdateLauncher launcher, ISettingsStore settings, IMessageBus bus, ILogger<UpdateService> logger)
    {
        this.releases = releases;
        this.launcher = launcher;
        this.settings = settings;
        this.bus = bus;
        this.logger = logger;
        CurrentVersion = ReleaseVersion.TryParse(launcher.CurrentVersion, out var version) ? version : null;
    }

    /// <summary>Raised after each change of <see cref="Status"/>, on the thread that made it.</summary>
    public event Action<UpdateStatus>? StatusChanged;

    /// <summary>Where the slice stands.</summary>
    public UpdateStatus Status
    {
        get
        {
            lock (statusLock)
            {
                return status;
            }
        }
    }

    /// <summary>The running version; <see langword="null"/> when the build carries none that can be read.</summary>
    public ReleaseVersion? CurrentVersion { get; }

    /// <summary>Whether this installation can update itself (not a development build).</summary>
    public bool CanInstall => launcher.CanInstall;

    /// <summary>
    /// Asks GitHub for a newer version. A check the user asked for shows its failure; an automatic one
    /// only logs it and keeps the previous status. Does nothing while another operation runs.
    /// </summary>
    /// <param name="automatic">Whether the background check started it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The status after the check.</returns>
    public async Task<UpdateStatus> CheckAsync(bool automatic = false, CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Status;
        }

        var before = Status;
        try
        {
            SetStatus(new UpdateStatus(UpdatePhase.Checking));
            var options = settings.Read(UpdateOptions.Section);
            var published = await releases.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
            var update = UpdateSelector.SelectNewest(published, CurrentVersion, options.IncludePreReleases);
            if (update is null)
            {
                LogUpToDate(CurrentVersion?.ToString() ?? "unknown");
                return SetStatus(new UpdateStatus(UpdatePhase.UpToDate));
            }

            LogFound(update.Version.ToString(), CurrentVersion?.ToString() ?? "unknown", update.Package.Name);
            SetStatus(new UpdateStatus(UpdatePhase.Available, update));
            if (automatic && !string.Equals(announced, update.Release.Tag, StringComparison.OrdinalIgnoreCase))
            {
                announced = update.Release.Tag;
                await bus.PublishAsync(new UpdateFound(update.Version.ToString()), cancellationToken).ConfigureAwait(false);
            }

            return Status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(before);
            throw;
        }
        catch (Exception exception)
        {
            // Offline, DNS, timeout, GitHub rate limit, unexpected answer: never thrown into the application.
            LogCheckFailed(automatic, exception);
            return SetStatus(automatic ? before : new UpdateStatus(UpdatePhase.Failed, before.Update, Failure: UpdateFailure.Unreachable));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Downloads and unpacks the version found by the last check, then starts the updater, which quits the
    /// application. Does nothing without an available version or while another operation runs.
    /// </summary>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The status after the attempt: <see cref="UpdatePhase.Installing"/> when the updater started.</returns>
    public async Task<UpdateStatus> InstallAsync(CancellationToken cancellationToken = default)
    {
        var update = Status.Update;
        if (update is null || !await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Status;
        }

        try
        {
            if (!launcher.CanInstall)
            {
                LogNotInstallable();
                return SetStatus(new UpdateStatus(UpdatePhase.Failed, update, Failure: UpdateFailure.NotInstallable));
            }

            SetStatus(new UpdateStatus(UpdatePhase.Downloading, update, Progress: 0));
            var progress = new Progress(this, update);
            string folder;
            try
            {
                folder = await releases.PrepareAsync(update.Package, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException exception)
            {
                LogPrepareFailed(update.Version.ToString(), exception);
                return SetStatus(new UpdateStatus(UpdatePhase.Failed, update, Failure: UpdateFailure.Corrupted));
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogPrepareFailed(update.Version.ToString(), exception);
                return SetStatus(new UpdateStatus(UpdatePhase.Failed, update, Failure: UpdateFailure.Unreachable));
            }

            SetStatus(new UpdateStatus(UpdatePhase.Installing, update));
            LogLaunching(update.Version.ToString(), folder);
            if (launcher.Launch(folder))
            {
                return Status;
            }

            LogLaunchFailed(folder);
            return SetStatus(new UpdateStatus(UpdatePhase.Failed, update, Failure: UpdateFailure.LaunchFailed));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(new UpdateStatus(UpdatePhase.Available, update));
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    private UpdateStatus SetStatus(UpdateStatus next)
    {
        lock (statusLock)
        {
            status = next;
        }

        try
        {
            StatusChanged?.Invoke(next);
        }
        catch (Exception exception)
        {
            // A faulty window must not break the update.
            LogListenerFailed(exception);
        }

        return next;
    }

    /// <summary>Turns download progress into status changes, at most one per percent.</summary>
    private sealed class Progress(UpdateService owner, AvailableUpdate update) : IProgress<double>
    {
        private int lastPercent = -1;

        public void Report(double value)
        {
            var percent = (int)Math.Floor(Math.Clamp(value, 0, 1) * 100);
            if (Interlocked.Exchange(ref lastPercent, percent) == percent || owner.Status.Phase != UpdatePhase.Downloading)
            {
                return;
            }

            owner.SetStatus(new UpdateStatus(UpdatePhase.Downloading, update, Progress: percent / 100.0));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: version {Version} is the newest.")]
    private partial void LogUpToDate(string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: version {Version} available (running {Current}, package {Package}).")]
    private partial void LogFound(string version, string current, string package);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed (automatic: {Automatic}).")]
    private partial void LogCheckFailed(bool automatic, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Development build: updates are not installed.")]
    private partial void LogNotInstallable();

    [LoggerMessage(Level = LogLevel.Error, Message = "Version {Version} could not be downloaded or unpacked.")]
    private partial void LogPrepareFailed(string version, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting the updater for version {Version} from {Folder}.")]
    private partial void LogLaunching(string version, string folder);

    [LoggerMessage(Level = LogLevel.Error, Message = "The updater in {Folder} could not be started.")]
    private partial void LogLaunchFailed(string folder);

    [LoggerMessage(Level = LogLevel.Error, Message = "A listener of the update status failed.")]
    private partial void LogListenerFailed(Exception exception);
}
