namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Where the updates slice stands.</summary>
public enum UpdatePhase
{
    /// <summary>Nothing checked since the start.</summary>
    Idle,

    /// <summary>Asking GitHub for the published releases.</summary>
    Checking,

    /// <summary>The running version is the newest.</summary>
    UpToDate,

    /// <summary>A newer version can be installed.</summary>
    Available,

    /// <summary>Downloading and unpacking the new version; the application keeps running.</summary>
    Downloading,

    /// <summary>The updater was started; the application is quitting.</summary>
    Installing,

    /// <summary>The last check or install failed; see <see cref="UpdateStatus.Failure"/>.</summary>
    Failed,
}

/// <summary>Why the last check or install failed.</summary>
public enum UpdateFailure
{
    /// <summary>GitHub could not be reached, or refused (no network, rate limit).</summary>
    Unreachable,

    /// <summary>The downloaded file does not match its digest, or is not a Kazo.Jarvis package.</summary>
    Corrupted,

    /// <summary>The updater could not be started.</summary>
    LaunchFailed,

    /// <summary>This installation cannot update itself (a development build).</summary>
    NotInstallable,
}

/// <summary>A snapshot of the updates slice, shown by the updates window.</summary>
/// <param name="Phase">Where it stands.</param>
/// <param name="Update">The newer version found, if any.</param>
/// <param name="Progress">Downloaded fraction, from 0 to 1, while downloading with a known size.</param>
/// <param name="Failure">Why it failed, in <see cref="UpdatePhase.Failed"/>.</param>
public sealed record UpdateStatus(
    UpdatePhase Phase,
    AvailableUpdate? Update = null,
    double? Progress = null,
    UpdateFailure? Failure = null)
{
    /// <summary>The state at start-up.</summary>
    public static UpdateStatus Initial { get; } = new(UpdatePhase.Idle);

    /// <summary>Whether a check or an install is running.</summary>
    public bool IsBusy => Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Installing;
}
