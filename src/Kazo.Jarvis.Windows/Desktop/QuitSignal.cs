namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Lets a service of the host ask the application to quit, as the Quit menu does (the updater launcher
/// does, once the updater started). <see cref="JarvisApp"/> listens and quits on the UI thread.
/// </summary>
internal sealed class QuitSignal
{
    /// <summary>Raised on the caller's thread.</summary>
    public event EventHandler? Requested;

    /// <summary>Asks the application to quit.</summary>
    public void Request() => Requested?.Invoke(this, EventArgs.Empty);
}
