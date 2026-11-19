namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// One instance per Windows session (ADR 0003, section 2). The first instance owns a named mutex; a second
/// launch signals a named event, which shows the orb of the first, then ends. <c>Local\</c> names are per
/// session, so another user signed in on the same PC has their own Jarvis.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Kazo.Jarvis.Instance";
    private const string ShowEventName = @"Local\Kazo.Jarvis.ShowOrb";

    private readonly Mutex mutex;
    private readonly EventWaitHandle showEvent;
    private RegisteredWaitHandle? wait;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent, bool isFirst)
    {
        this.mutex = mutex;
        this.showEvent = showEvent;
        IsFirst = isFirst;
    }

    /// <summary>Whether this process is the instance that runs.</summary>
    public bool IsFirst { get; }

    /// <summary>Claims the instance. Call on the main thread, which releases it.</summary>
    /// <returns>The claim; check <see cref="IsFirst"/>.</returns>
    public static SingleInstance Claim()
    {
        // The event exists before the mutex, so a second instance always finds it.
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            try
            {
                // Free again when the previous owner ended.
                created = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous instance died without releasing it: this one takes over.
                created = true;
            }
        }

        return new SingleInstance(mutex, showEvent, created);
    }

    /// <summary>Asks the running instance to show its orb (second launch).</summary>
    public void SignalFirst() => showEvent.Set();

    /// <summary>Calls <paramref name="action"/> on a pool thread each time a second launch signals.</summary>
    /// <param name="action">What to do; it dispatches to the UI thread itself.</param>
    public void OnShowRequested(Action action) =>
        wait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => action(), null, Timeout.Infinite, executeOnlyOnce: false);

    /// <inheritdoc />
    public void Dispose()
    {
        wait?.Unregister(null);
        if (IsFirst)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        showEvent.Dispose();
    }
}
