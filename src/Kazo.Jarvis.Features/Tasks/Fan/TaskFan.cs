using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Tasks.Fan;

/// <summary>Whether the fan is out.</summary>
public enum FanPhase
{
    /// <summary>Put away: nothing is drawn, no timer runs (ADR 0010, section 5).</summary>
    Closed,

    /// <summary>Out: a still image once its opening is over (ADR 0010, section 6).</summary>
    Open,

    /// <summary>Being drawn back into the orb; closed once <see cref="TaskFan.ClosingTime"/> has passed.</summary>
    Closing,
}

/// <summary>
/// The task fan, open or closed (ADR 0010). A double click on the orb opens it, a second one puts it away, and
/// so do Escape and a click on the darkened desktop. It is not a state of the assistant: open or closed, it
/// changes neither the phase nor the conditions of ADR 0005.
/// </summary>
public sealed class TaskFan : IEventHandler<TaskFanToggleRequested>
{
    /// <summary>How long the panels take to come out (spike E11).</summary>
    public static readonly TimeSpan OpeningTime = TimeSpan.FromMilliseconds(400);

    /// <summary>How long the panels take to go back in (spike E11).</summary>
    public static readonly TimeSpan ClosingTime = TimeSpan.FromMilliseconds(300);

    private readonly ITaskFanSurface? surface;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private FanPhase phase;
    private FanAnchor anchor = FanAnchor.BottomRight;
    private int generation;

    /// <summary>Creates the fan, closed.</summary>
    /// <param name="time">The clock of the closing.</param>
    /// <param name="surface">Where the fan is drawn; a host without one has no fan (ADR 0010, section 4).</param>
    public TaskFan(TimeProvider time, ITaskFanSurface? surface = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        this.time = time;
        this.surface = surface;
    }

    /// <summary>Raised after the fan opened, started to close or closed, on any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether the fan is out.</summary>
    public FanPhase Phase
    {
        get
        {
            lock (gate)
            {
                return phase;
            }
        }
    }

    /// <summary>Where the orb was on the surface when the fan last opened.</summary>
    public FanAnchor Anchor
    {
        get
        {
            lock (gate)
            {
                return anchor;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TaskFanToggleRequested message, CancellationToken cancellationToken)
    {
        Toggle();
        return ValueTask.CompletedTask;
    }

    /// <summary>Opens the fan when it is closed or closing, puts it away when it is open.</summary>
    public void Toggle()
    {
        if (Phase == FanPhase.Open)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Opens the fan from where the orb is now. Does nothing when it is open, or without a surface.</summary>
    public void Open()
    {
        if (surface is null)
        {
            return;
        }

        var read = surface.Anchor;
        lock (gate)
        {
            if (phase == FanPhase.Open)
            {
                return;
            }

            phase = FanPhase.Open;
            anchor = read;
            generation++;
        }

        surface.Show();
        Changed?.Invoke();
    }

    /// <summary>Puts the fan away: the panels go back into the orb, then the surface disappears. Does nothing unless it is open.</summary>
    public void Close()
    {
        int mine;
        lock (gate)
        {
            if (phase != FanPhase.Open)
            {
                return;
            }

            phase = FanPhase.Closing;
            mine = ++generation;
        }

        Changed?.Invoke();
        _ = FinishAsync(mine);
    }

    private async Task FinishAsync(int mine)
    {
        await Task.Delay(ClosingTime, time).ConfigureAwait(false);
        lock (gate)
        {
            // Opened again meanwhile: the surface stays.
            if (generation != mine || phase != FanPhase.Closing)
            {
                return;
            }

            phase = FanPhase.Closed;
        }

        surface?.Hide();
        Changed?.Invoke();
    }
}
