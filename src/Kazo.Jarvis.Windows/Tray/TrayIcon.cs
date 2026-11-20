using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Audio;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Windows.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows.Tray;

/// <summary>
/// The tray icon (US1.2): left click shows or hides the orb; the menu offers Settings, Conversation, Mute the
/// microphone, Updates (US1.18) and Quit. A notice tells about a new version; clicking it opens Updates. Settings, Conversation and Mute the microphone are greyed out until a slice handles their event; Mute the
/// microphone is ticked while the microphone is muted (US1.13). Created and used on the UI thread.
/// </summary>
internal sealed partial class TrayIcon : IDisposable
{
    // NotifyIcon.Text is limited to 127 characters.
    private const int MaximumTooltip = 127;

    private readonly IOrbWindow orb;
    private readonly IMessageBus bus;
    private readonly IAssistantStateReader state;
    private readonly IServiceProvider services;
    private readonly ILogger<TrayIcon> logger;
    private NotifyIcon? icon;
    private Icon? image;

    /// <summary>Creates the tray icon; nothing shows before <see cref="Show"/>.</summary>
    public TrayIcon(IOrbWindow orb, IMessageBus bus, IAssistantStateReader state, IServiceProvider services, ILogger<TrayIcon> logger)
    {
        this.orb = orb;
        this.bus = bus;
        this.state = state;
        this.services = services;
        this.logger = logger;
    }

    /// <summary>Raised when the user chooses Quit.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Shows the icon.</summary>
    public void Show()
    {
        if (icon is not null)
        {
            icon.Visible = true;
            return;
        }

        image = DrawIcon();
        icon = new NotifyIcon
        {
            Icon = image,
            Text = HostTexts.ApplicationName,
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };
        icon.BalloonTipClicked += (_, _) => Publish(new UpdatesWindowRequested());
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                orb.ToggleVisibility();
            }
        };
    }

    /// <summary>Adds a status to the tooltip, or removes it when <paramref name="status"/> is <see langword="null"/>.</summary>
    /// <param name="status">A short status, already translated.</param>
    public void SetStatus(string? status)
    {
        if (icon is null)
        {
            return;
        }

        var text = status is null ? HostTexts.ApplicationName : HostTexts.WithStatus(status);
        icon.Text = text.Length > MaximumTooltip ? text[..MaximumTooltip] : text;
    }

    /// <summary>Tells the user that <paramref name="version"/> can be installed. Clicking the notice opens Updates.</summary>
    /// <param name="version">The new version.</param>
    public void ShowUpdateNotice(string version)
    {
        if (icon is null)
        {
            return;
        }

        icon.ShowBalloonTip(10_000, HostTexts.ApplicationName, HostTexts.UpdateFound(version), ToolTipIcon.Info);
    }

    /// <summary>Removes the icon from the tray.</summary>
    public void Hide()
    {
        if (icon is not null)
        {
            icon.Visible = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        icon?.Dispose();
        image?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var settingsItem = new ToolStripMenuItem(HostTexts.Settings, null, (_, _) => Publish(new SettingsWindowRequested()));
        var conversationItem = new ToolStripMenuItem(HostTexts.Conversation, null, (_, _) => Publish(new ConversationWindowRequested()));
        var muteItem = new ToolStripMenuItem(HostTexts.MuteMicrophone, null, (_, _) => Publish(new MicrophoneMuteRequested(!state.Current.MicrophoneMuted)));
        var updatesItem = new ToolStripMenuItem(HostTexts.Updates, null, (_, _) => Publish(new UpdatesWindowRequested()));
        var setupItem = new ToolStripMenuItem(HostTexts.FirstRunWizard, null, (_, _) => Publish(new FirstRunWizardRequested()));
        var quitItem = new ToolStripMenuItem(HostTexts.Quit, null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([settingsItem, conversationItem, muteItem, updatesItem, setupItem, new ToolStripSeparator(), quitItem]);
        menu.Opening += (_, _) =>
        {
            // Read the texts again: the assistant language may have changed since the last opening (ADR 0008).
            settingsItem.Text = HostTexts.Settings;
            conversationItem.Text = HostTexts.Conversation;
            muteItem.Text = HostTexts.MuteMicrophone;
            updatesItem.Text = HostTexts.Updates;
            setupItem.Text = HostTexts.FirstRunWizard;
            quitItem.Text = HostTexts.Quit;
            settingsItem.Enabled = HasHandler<SettingsWindowRequested>();
            conversationItem.Enabled = HasHandler<ConversationWindowRequested>();
            muteItem.Enabled = HasHandler<MicrophoneMuteRequested>();
            muteItem.Checked = state.Current.MicrophoneMuted;
            updatesItem.Enabled = HasHandler<UpdatesWindowRequested>();
            setupItem.Enabled = HasHandler<FirstRunWizardRequested>();
        };
        return menu;
    }

    private bool HasHandler<TEvent>()
        where TEvent : IEvent => services.GetServices<IEventHandler<TEvent>>().Any();

    private void Publish<TEvent>(TEvent message)
        where TEvent : IEvent => _ = Task.Run(async () =>
    {
        try
        {
            await bus.PublishAsync(message).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogPublishFailed(typeof(TEvent).Name, exception);
        }
    });

    /// <summary>A provisional icon: a cyan disc, like the provisional orb.</summary>
    private static Icon DrawIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var glow = new SolidBrush(Color.FromArgb(90, 79, 216, 255));
            using var core = new SolidBrush(Color.FromArgb(255, 79, 216, 255));
            graphics.FillEllipse(glow, 1, 1, 30, 30);
            graphics.FillEllipse(core, 6, 6, 20, 20);
        }

        // Icon.FromHandle does not own the handle: clone it into a managed icon, then free the handle.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [LoggerMessage(Level = LogLevel.Error, Message = "The tray menu event {Event} could not be published.")]
    private partial void LogPublishFailed(string @event, Exception exception);
}
