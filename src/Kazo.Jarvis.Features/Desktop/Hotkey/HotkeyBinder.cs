using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Desktop.Hotkey;

/// <summary>
/// Background service of the hotkey slice (US1.6): registers the configured hotkey, registers it again when
/// the setting changes, and turns each press into <see cref="ActivationRequested"/> for the state machine.
/// </summary>
public sealed partial class HotkeyBinder : IHostedService, IDisposable
{
    private readonly IGlobalHotkey hotkey;
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly ILogger<HotkeyBinder> logger;
    private IDisposable? watch;

    /// <summary>Creates the binder.</summary>
    /// <param name="hotkey">The host's global hotkey.</param>
    /// <param name="settings">The settings, for <see cref="HotkeyOptions.Section"/>.</param>
    /// <param name="bus">Where the activation goes.</param>
    /// <param name="logger">Logs the registration result.</param>
    public HotkeyBinder(IGlobalHotkey hotkey, ISettingsStore settings, IMessageBus bus, ILogger<HotkeyBinder> logger)
    {
        this.hotkey = hotkey;
        this.settings = settings;
        this.bus = bus;
        this.logger = logger;
    }

    /// <summary>The result of the last registration, <see langword="null"/> before the start.</summary>
    public HotkeyRegistration? LastResult { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        watch = settings.Watch(HotkeyOptions.Section, Apply);
        Apply(settings.Read(HotkeyOptions.Section));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        watch?.Dispose();
        watch = null;
        hotkey.Unregister();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => watch?.Dispose();

    private void Apply(HotkeyOptions options)
    {
        // The store validates before saving; a hand-edited file can still hold a bad value.
        if (!HotkeyGesture.TryParse(options.Gesture, out var gesture))
        {
            LogInvalid(options.Gesture, HotkeyOptions.DefaultGesture);
            gesture = HotkeyGesture.Parse(HotkeyOptions.DefaultGesture);
        }

        LastResult = hotkey.Register(gesture, OnPressed);
        if (LastResult == HotkeyRegistration.Registered)
        {
            LogRegistered(gesture);
        }
        else
        {
            LogNotRegistered(gesture, LastResult.Value);
        }
    }

    private void OnPressed()
    {
        // The host calls back on its UI thread: the bus delivery must not hold it.
        _ = Task.Run(async () =>
        {
            try
            {
                await bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogPublishFailed(exception);
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Global hotkey {Gesture} registered.")]
    private partial void LogRegistered(HotkeyGesture gesture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Global hotkey {Gesture} not registered: {Result}.")]
    private partial void LogNotRegistered(HotkeyGesture gesture, HotkeyRegistration result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The hotkey setting '{Gesture}' is not valid; {Fallback} is used instead.")]
    private partial void LogInvalid(string gesture, string fallback);

    [LoggerMessage(Level = LogLevel.Error, Message = "The hotkey activation could not be published.")]
    private partial void LogPublishFailed(Exception exception);
}
