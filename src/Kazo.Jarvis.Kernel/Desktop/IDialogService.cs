namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// Opens a slice component in its own window (ADR 0003, sections 2 and 5): a normal window in the Windows
/// host, every window on the same service provider; a layer over the page in the Web host. One window per
/// key. Callable from any thread.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Opens <paramref name="request"/>, or brings forward the window already open under its key (the
    /// component and parameters of the first request stay).
    /// </summary>
    /// <param name="request">What to open.</param>
    void Open(DialogRequest request);

    /// <summary>Closes the window of <paramref name="key"/>. Does nothing when none is open.</summary>
    /// <param name="key">The key given when it was opened.</param>
    void Close(string key);

    /// <summary>Whether a window is open under <paramref name="key"/>.</summary>
    /// <param name="key">The key given when it was opened.</param>
    /// <returns><see langword="true"/> when it is open.</returns>
    bool IsOpen(string key);
}
