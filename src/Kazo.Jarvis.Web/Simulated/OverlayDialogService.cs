using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IDialogService"/>: the Web host cannot open windows, so dialogs show as layers over
/// the page (ADR 0003, section 3), drawn by <c>DialogOverlay</c>. One layer per key.
/// </summary>
public sealed class OverlayDialogService : IDialogService
{
    private readonly Lock gate = new();
    private readonly List<DialogRequest> open = [];

    /// <summary>Raised after a dialog opened or closed, on any thread.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event Action<string>? Closed;

    /// <summary>The open dialogs, oldest first.</summary>
    public IReadOnlyList<DialogRequest> OpenDialogs
    {
        get
        {
            lock (gate)
            {
                return [.. open];
            }
        }
    }

    /// <inheritdoc />
    public void Open(DialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            if (open.Any(dialog => dialog.Key == request.Key))
            {
                return;
            }

            open.Add(request);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Close(string key)
    {
        int removed;
        lock (gate)
        {
            removed = open.RemoveAll(dialog => dialog.Key == key);
        }

        if (removed > 0)
        {
            Closed?.Invoke(key);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public bool IsOpen(string key)
    {
        lock (gate)
        {
            return open.Any(dialog => dialog.Key == key);
        }
    }
}
