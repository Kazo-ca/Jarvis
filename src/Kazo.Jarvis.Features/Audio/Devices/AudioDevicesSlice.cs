using Kazo.Jarvis.Kernel.Messages.Audio;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>Registration of the audio devices slice (US1.13, ADR 0003 and 0006).</summary>
public static class AudioDevicesSlice
{
    /// <summary>Key of the audio page in the settings window.</summary>
    public const string PageKey = "Audio.Devices";

    /// <summary>
    /// Adds the microphone service (capture, mute, fault), the output binder and the settings page. The host
    /// provides <see cref="Kernel.Audio.IAudioCapture"/> and <see cref="Kernel.Audio.IAudioPlayback"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAudioDevicesSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<MicrophoneService>();
        services.AddHostedService(provider => provider.GetRequiredService<MicrophoneService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<MicrophoneMuteRequested>, MicrophoneService>(
            provider => provider.GetRequiredService<MicrophoneService>()));

        services.TryAddSingleton<OutputBinder>();
        services.AddHostedService(provider => provider.GetRequiredService<OutputBinder>());

        // After the language page (order 0).
        services.AddSingleton(new SettingsPage(PageKey, typeof(AudioSettingsPage), typeof(AudioSettingsPage), "Title", Order: 10));
        return services;
    }
}
