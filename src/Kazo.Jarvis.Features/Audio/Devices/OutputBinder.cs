using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>
/// Background service of the audio slice: gives the chosen output to the host's playback at start-up and
/// after each change, so every slice that plays a sound (voice, test page) uses it without reading this
/// slice's settings.
/// </summary>
public sealed class OutputBinder : IHostedService, IDisposable
{
    private readonly IAudioPlayback playback;
    private readonly ISettingsStore settings;
    private IDisposable? watch;

    /// <summary>Creates the binder.</summary>
    /// <param name="playback">The host's audio playback.</param>
    /// <param name="settings">The settings, for <see cref="AudioDevicesOptions.Section"/>.</param>
    public OutputBinder(IAudioPlayback playback, ISettingsStore settings)
    {
        this.playback = playback;
        this.settings = settings;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        watch = settings.Watch(AudioDevicesOptions.Section, Apply);
        Apply(settings.Read(AudioDevicesOptions.Section));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        watch?.Dispose();
        watch = null;
        playback.StopAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => watch?.Dispose();

    private void Apply(AudioDevicesOptions options)
    {
        if (playback.SelectedOutput != options.OutputId)
        {
            playback.SelectOutput(options.OutputId);
        }
    }
}
