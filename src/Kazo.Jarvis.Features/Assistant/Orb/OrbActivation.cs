using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// What the gestures on the orb do (US1.6; ADR 0027). A left click publishes an activation, and the state machine
/// decides (start listening, cancel it, or interrupt Jarvis). A right click asks for the task fan, at once. A
/// double click asks for the projects view and is never also a single click: a left click waits for the
/// double-click delay of the host before it acts, and a second click inside that delay replaces it. As long as
/// no slice answers <see cref="ProjectsViewRequested"/> there is neither double click nor delay. The wake word
/// and the hotkey do not come through here and are not delayed.
/// </summary>
/// <remarks>
/// The orb component calls it, and so does the Windows host, whose window catches the press itself to tell a
/// click from a move (ADR 0003, section 2). Both call <see cref="ClickAsync"/> once per left click and
/// <see cref="RightClickAsync"/> once per right click.
/// </remarks>
public sealed class OrbActivation
{
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly TimeSpan delay;
    private readonly Lock gate = new();
    private CancellationTokenSource? waiting;

    /// <summary>Creates the activation.</summary>
    /// <param name="bus">Where the activation and the requests for the fan and the projects view are published.</param>
    /// <param name="time">The clock of the delay.</param>
    /// <param name="timings">The double-click delay of the host; without it there is no double click and a click acts at once.</param>
    /// <param name="projects">
    /// The slices that answer a double click. While there is none, there is no double click either: nothing would
    /// answer it, and every left click would wait for nothing (ADR 0027, section 3).
    /// </param>
    public OrbActivation(
        IMessageBus bus,
        TimeProvider time,
        IPointerTimings? timings = null,
        IEnumerable<IEventHandler<ProjectsViewRequested>>? projects = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(time);
        this.bus = bus;
        this.time = time;
        delay = projects?.Any() == true ? timings?.DoubleClickTime ?? TimeSpan.Zero : TimeSpan.Zero;
    }

    /// <summary>One right click on the orb: publishes <see cref="TaskFanToggleRequested"/> at once.</summary>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes once the request was published.</returns>
    public ValueTask RightClickAsync(CancellationToken cancellationToken = default) =>
        bus.PublishAsync(new TaskFanToggleRequested(), cancellationToken);

    /// <summary>
    /// One left click on the orb. The first click waits for the delay, then publishes
    /// <see cref="ActivationRequested"/>; a second click inside the delay publishes
    /// <see cref="ProjectsViewRequested"/> instead, and the first one publishes nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes once the click was treated: published, or replaced by a double click.</returns>
    public async ValueTask ClickAsync(CancellationToken cancellationToken = default)
    {
        if (delay <= TimeSpan.Zero)
        {
            await bus.PublishAsync(new ActivationRequested(ActivationSource.Click), cancellationToken).ConfigureAwait(false);
            return;
        }

        var mine = new CancellationTokenSource();
        bool second;
        lock (gate)
        {
            second = waiting is not null;
            if (second)
            {
                // Cancelled under the lock: the first click disposes its source only after it took the lock again.
                waiting!.Cancel();
                waiting = null;
            }
            else
            {
                waiting = mine;
            }
        }

        if (second)
        {
            mine.Dispose();
            await bus.PublishAsync(new ProjectsViewRequested(), cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await Task.Delay(delay, time, mine.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A second click arrived: this one was half of a double click.
        }

        bool single;
        lock (gate)
        {
            single = ReferenceEquals(waiting, mine);
            if (single)
            {
                waiting = null;
            }
        }

        mine.Dispose();
        if (single)
        {
            await bus.PublishAsync(new ActivationRequested(ActivationSource.Click), cancellationToken).ConfigureAwait(false);
        }
    }
}
