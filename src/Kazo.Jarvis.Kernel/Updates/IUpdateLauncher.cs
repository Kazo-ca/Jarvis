namespace Kazo.Jarvis.Kernel.Updates;

/// <summary>
/// Hands a prepared version over to the updater (US1.18, ADR 0001, section 3): the host starts
/// <c>Kazo.Jarvis.Updater.exe</c> from the prepared folder, then quits; the updater waits for the end of
/// the application, replaces its files and starts it again. The Web host only records the request.
/// </summary>
public interface IUpdateLauncher
{
    /// <summary>The version of the running application, as built (the <c>Version</c> MSBuild property).</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Whether this installation can update itself. <see langword="false"/> for a development build (run from
    /// <c>bin\Debug</c> or <c>bin\Release</c>), so that a check never overwrites a branch being tried.
    /// </summary>
    bool CanInstall { get; }

    /// <summary>Starts the updater on <paramref name="preparedFolder"/>, then quits the application.</summary>
    /// <param name="preparedFolder">The folder returned by <see cref="IReleaseSource.PrepareAsync"/>.</param>
    /// <returns><see langword="true"/> when the updater started (the application is quitting); <see langword="false"/> when it could not.</returns>
    bool Launch(string preparedFolder);
}
