namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// Opens a slice component in its own window (ADR 0003, sections 2 and 5): a normal window in the Windows
/// host, every window on the same service provider; a layer over the page in the Web host. One window per
/// key. Callable from any thread.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Raised with its key after a window closed, whoever closed it: the user or <see cref="Close"/>. On any
    /// thread. A slice that reads the user closing its window as an answer listens here: the end of a component
    /// says nothing, since a view can go away without the user closing anything.
    /// </summary>
    event Action<string>? Closed;

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
