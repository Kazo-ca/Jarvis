using System.Windows.Threading;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Features.Tasks.Fan;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Windows.Orb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows.Fan;

/// <summary>
/// <see cref="ITaskFanSurface"/> of the Windows host (ADR 0010, section 5). The fan window is created once, by
/// <see cref="Prepare"/> after the orb is drawn, and kept hidden: opening the fan then only shows it. The orb
/// window is not enlarged; it stays above the fan.
/// </summary>
internal sealed partial class FanWindowService : ITaskFanSurface
{
    private readonly IServiceProvider services;
    private readonly Dispatcher dispatcher;
    private readonly OrbWindowService orb;
    private readonly JarvisPaths paths;
    private readonly ILogger<FanWindowService> logger;
    private FanWindow? window;
    private bool shown;
    private bool closed;

    /// <summary>Creates the service. The window is not created yet.</summary>
    public FanWindowService(
        IServiceProvider services,
        Dispatcher dispatcher,
        OrbWindowService orb,
        JarvisPaths paths,
        ILogger<FanWindowService> logger)
    {
        this.services = services;
        this.dispatcher = dispatcher;
        this.orb = orb;
        this.paths = paths;
        this.logger = logger;
    }

    /// <inheritdoc />
    public FanAnchor Anchor => dispatcher.Invoke(() =>
    {
        var area = Area(out var centre);
        return centre is { } point && area.Width > 0 && area.Height > 0
            ? new FanAnchor(
                Math.Clamp((point.X - area.X) / (double)area.Width, 0, 1),
                Math.Clamp((point.Y - area.Y) / (double)area.Height, 0, 1))
            : FanAnchor.BottomRight;
    });

    /// <summary>
    /// Creates the window and its web view ahead of the first opening. The window is shown once, empty, so that
    /// the web view starts, then hidden as soon as the web view exists. UI thread.
    /// </summary>
    public void Prepare()
    {
        dispatcher.VerifyAccess();
        if (window is not null || closed)
        {
            return;
        }

        try
        {
            var created = Create();
            Place(created);
            created.Show();
        }
        catch (Exception exception)
        {
            // The fan is a comfort: without it the orb, the voice and the confirmation window still work.
            LogPrepareFailed(exception);
            Drop();
        }
    }

    /// <inheritdoc />
    public void Show() => dispatcher.Invoke(() =>
    {
        if (closed)
        {
            return;
        }

        shown = true;
        var fan = window ?? Create();
        Place(fan);
        fan.Show();

        // The keyboard focus, for Escape; then the orb back above, where the next double click is expected.
        fan.Activate();
        orb.BringToTop();
    });

    /// <inheritdoc />
    public void Hide() => dispatcher.Invoke(() =>
    {
        shown = false;
        window?.Hide();
    });

    /// <summary>Closes and forgets the window: the desktop was lost, and the next opening creates a new one. UI thread.</summary>
    public void Drop()
    {
        dispatcher.VerifyAccess();
        var closing = window;
        window = null;
        closing?.Close();
    }

    /// <summary>Closes the window for good: the application quits. UI thread.</summary>
    public void Close()
    {
        closed = true;
        Drop();
    }

    private FanWindow Create()
    {
        var created = new FanWindow(services, paths.WebView);
        created.Ready += (_, _) => dispatcher.BeginInvoke(() =>
        {
            // Prepared at start-up: hidden again unless the fan was opened meanwhile.
            if (!shown && ReferenceEquals(window, created))
            {
                created.Hide();
            }
        });

        // A click on a panel activates the fan window, which passes over the orb (spike E11).
        created.Activated += (_, _) => orb.BringToTop();

        // The fan slice owns "open or closed": Escape goes through it, like a click on the darkened desktop.
        created.EscapePressed += (_, _) => services.GetRequiredService<TaskFan>().Close();
        window = created;
        return created;
    }

    private void Place(FanWindow fan)
    {
        var area = Area(out _);
        fan.Cover(area.X, area.Y, area.Width, area.Height);
        LogPlaced(area.X, area.Y, area.Width, area.Height);
    }

    // The work area of the screen the orb is on, in physical pixels, and the centre of the orb.
    private System.Drawing.Rectangle Area(out System.Drawing.Point? centre)
    {
        if (orb.ScreenBounds is { Width: > 0, Height: > 0 } bounds)
        {
            var point = new System.Drawing.Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            centre = point;
            return System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
        }

        centre = null;
        return (System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0]).WorkingArea;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fan window placed at ({X}, {Y}), {Width} x {Height} screen pixels.")]
    private partial void LogPlaced(int x, int y, int width, int height);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The fan window could not be prepared; it will be created when the fan first opens.")]
    private partial void LogPrepareFailed(Exception exception);
}
