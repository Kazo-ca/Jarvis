using System.Windows;
using System.Windows.Threading;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows.Orb;

/// <summary>
/// <see cref="IOrbWindow"/> of the Windows host. The window itself is created by <see cref="Open"/> once the
/// desktop can show it (the session may be locked at start-up); until then <see cref="Show"/> and
/// <see cref="Hide"/> only record what the user wants.
/// </summary>
internal sealed partial class OrbWindowService : IOrbWindow
{
    private const double ScreenMargin = 24;
    private const double MinimumVisible = 40;

    private readonly IServiceProvider services;
    private readonly Dispatcher dispatcher;
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly JarvisPaths paths;
    private readonly ILogger<OrbWindowService> logger;
    private OrbWindow? window;
    private volatile bool wanted = true;

    /// <summary>Creates the service. The window is not created yet.</summary>
    public OrbWindowService(
        IServiceProvider services,
        Dispatcher dispatcher,
        ISettingsStore settings,
        IMessageBus bus,
        JarvisPaths paths,
        ILogger<OrbWindowService> logger)
    {
        this.services = services;
        this.dispatcher = dispatcher;
        this.settings = settings;
        this.bus = bus;
        this.paths = paths;
        this.logger = logger;
    }

    /// <inheritdoc />
    public bool IsVisible => wanted;

    /// <inheritdoc />
    public event EventHandler? VisibilityChanged;

    /// <summary>Raised on the UI thread the first time the orb window is drawn after <see cref="Open"/>.</summary>
    public event EventHandler? Rendered;

    /// <summary>Whether the window exists.</summary>
    public bool IsOpen => window is not null;

    /// <summary>Creates the window at its remembered position, and shows it when the user wants it. UI thread.</summary>
    public void Open()
    {
        dispatcher.VerifyAccess();
        if (window is not null)
        {
            return;
        }

        window = new OrbWindow(services, paths.WebView);
        (window.Left, window.Top) = InitialPosition(settings.Read(OrbPlacement.Section));
        window.Clicked += OnClicked;
        window.Moved += OnMoved;
        window.ContentRendered += OnContentRendered;
        if (wanted)
        {
            window.Show();
        }

        LogOpened(window.Left, window.Top);
    }

    /// <summary>Closes and forgets the window (it failed, or the application quits). UI thread.</summary>
    public void Close()
    {
        dispatcher.VerifyAccess();
        var closing = window;
        window = null;
        closing?.Close();
    }

    /// <inheritdoc />
    public void Show() => SetWanted(true);

    /// <inheritdoc />
    public void Hide() => SetWanted(false);

    /// <inheritdoc />
    public void ToggleVisibility() => dispatcher.Invoke(() => SetWanted(!wanted));

    private void SetWanted(bool visible) => dispatcher.Invoke(() =>
    {
        var changed = wanted != visible;
        wanted = visible;
        if (window is not null)
        {
            if (visible)
            {
                window.Show();
                window.Topmost = true;
            }
            else
            {
                window.Hide();
            }
        }

        if (changed)
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    });

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (sender is OrbWindow rendered)
        {
            rendered.ContentRendered -= OnContentRendered;
        }

        Rendered?.Invoke(this, EventArgs.Empty);
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        // Provisional: the orb slice (stage1/6-orb) takes the click over, with its component.
        _ = Task.Run(async () =>
        {
            try
            {
                await bus.PublishAsync(new ActivationRequested(ActivationSource.Click)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogPublishFailed(exception);
            }
        });
    }

    private void OnMoved(object? sender, EventArgs e)
    {
        if (window is null)
        {
            return;
        }

        var placement = new OrbPlacement { Left = window.Left, Top = window.Top };
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await settings.WriteAsync(OrbPlacement.Section, placement).ConfigureAwait(false);
                LogMoved(placement.Left ?? 0, placement.Top ?? 0, result.Outcome);
            }
            catch (Exception exception)
            {
                LogSaveFailed(exception);
            }
        });
    }

    /// <summary>The remembered position when it is still on a screen; the bottom right corner of the work area otherwise.</summary>
    private static (double Left, double Top) InitialPosition(OrbPlacement placement)
    {
        if (placement is { Left: { } left, Top: { } top }
            && left + OrbWindow.Size >= SystemParameters.VirtualScreenLeft + MinimumVisible
            && left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - MinimumVisible
            && top + OrbWindow.Size >= SystemParameters.VirtualScreenTop + MinimumVisible
            && top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - MinimumVisible)
        {
            return (left, top);
        }

        var area = SystemParameters.WorkArea;
        return (area.Right - OrbWindow.Size - ScreenMargin, area.Bottom - OrbWindow.Size - ScreenMargin);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Orb window opened at ({Left}, {Top}).")]
    private partial void LogOpened(double left, double top);

    [LoggerMessage(Level = LogLevel.Information, Message = "Orb window moved to ({Left}, {Top}); position {Outcome}.")]
    private partial void LogMoved(double left, double top, SettingsWriteOutcome outcome);

    [LoggerMessage(Level = LogLevel.Error, Message = "The orb position could not be saved.")]
    private partial void LogSaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The orb click could not be published.")]
    private partial void LogPublishFailed(Exception exception);
}
