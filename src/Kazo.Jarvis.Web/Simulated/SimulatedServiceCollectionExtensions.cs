using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Updates;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>Registers the simulated contracts of the Web host (ADR 0003, section 3).</summary>
public static class SimulatedServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory settings, document store and vault, and the simulated desktop services (orb, dialogs,
    /// hotkey, start with Windows), the audio devices, the Windows display language, the simulated release source and updater launcher, the simulated language model connector, the simulated wake word, transcription and voices, the simulated model downloads and graphics card. Called before <c>AddJarvisFeatures</c>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisSimulatedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.TryAddSingleton<IDocumentStore>(provider => new InMemoryDocumentStore(provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<InMemorySecretVault>();
        services.TryAddSingleton<ISecretVault>(provider => provider.GetRequiredService<InMemorySecretVault>());
        services.TryAddSingleton<ISecretVaultSelector>(provider => provider.GetRequiredService<InMemorySecretVault>());

        services.TryAddSingleton<SimulatedOrbWindow>();
        services.TryAddSingleton<IOrbWindow>(provider => provider.GetRequiredService<SimulatedOrbWindow>());
        services.TryAddSingleton<OverlayDialogService>();
        services.TryAddSingleton<IDialogService>(provider => provider.GetRequiredService<OverlayDialogService>());
        services.TryAddSingleton<SimulatedGlobalHotkey>();
        services.TryAddSingleton<IGlobalHotkey>(provider => provider.GetRequiredService<SimulatedGlobalHotkey>());
        services.TryAddSingleton<ILaunchAtLogin, InMemoryLaunchAtLogin>();
        services.TryAddSingleton<ISystemLanguage, SimulatedSystemLanguage>();

        services.TryAddSingleton<SimulatedAudioCapture>();
        services.TryAddSingleton<IAudioCapture>(provider => provider.GetRequiredService<SimulatedAudioCapture>());
        services.TryAddSingleton<SimulatedAudioPlayback>();
        services.TryAddSingleton<IAudioPlayback>(provider => provider.GetRequiredService<SimulatedAudioPlayback>());

        services.TryAddSingleton<SimulatedReleaseSource>();
        services.TryAddSingleton<IReleaseSource>(provider => provider.GetRequiredService<SimulatedReleaseSource>());
        services.TryAddSingleton<SimulatedUpdateLauncher>();
        services.TryAddSingleton<IUpdateLauncher>(provider => provider.GetRequiredService<SimulatedUpdateLauncher>());

        services.TryAddSingleton<SimulatedSpeechEngine>();
        services.TryAddSingleton<SimulatedCloudSpeechEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, SimulatedSpeechEngine>(provider => provider.GetRequiredService<SimulatedSpeechEngine>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, SimulatedCloudSpeechEngine>(provider => provider.GetRequiredService<SimulatedCloudSpeechEngine>()));

        services.TryAddSingleton<SimulatedLlmConnector>();
        services.TryAddSingleton<ILlmConnector>(provider => provider.GetRequiredService<SimulatedLlmConnector>());

        services.TryAddSingleton<SimulatedWakeWordEngine>();
        services.TryAddSingleton<IWakeWordEngine>(provider => provider.GetRequiredService<SimulatedWakeWordEngine>());
        services.TryAddSingleton<SimulatedSpeechRecognition>();
        services.TryAddSingleton<ISpeechRecognition>(provider => provider.GetRequiredService<SimulatedSpeechRecognition>());
        // Two tools that need a confirmation, to show the request before a real tool does (stage2/4-confirmation).
        services.TryAddSingleton<SimulatedNotes>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Kazo.Jarvis.Kernel.Tools.ITool, SimulatedNoteTool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Kazo.Jarvis.Kernel.Tools.ITool, SimulatedEraseTool>());
        // Basic tools (stage2/5-basic-tools): a few files in memory and a browser with fixed pages; the address
        // policy comes from the web tools slice.
        // Claude Code sessions (stage2/11-tasks-and-sessions): a few sessions in memory, moved by the test page.
        services.TryAddSingleton<SimulatedClaudeSessions>();
        services.TryAddSingleton<Kazo.Jarvis.Kernel.ClaudeCode.IClaudeSessionRegistry>(provider => provider.GetRequiredService<SimulatedClaudeSessions>());

        services.TryAddSingleton<SimulatedLocalFiles>();
        services.TryAddSingleton<Kazo.Jarvis.Kernel.Files.ILocalFiles>(provider => provider.GetRequiredService<SimulatedLocalFiles>());
        services.TryAddSingleton<SimulatedReadingBrowser>();
        services.TryAddSingleton<Kazo.Jarvis.Kernel.Web.IReadingBrowser>(provider => provider.GetRequiredService<SimulatedReadingBrowser>());
        // Tool servers (stage2/6-mcp-client): fixed servers in memory.
        services.TryAddSingleton<SimulatedToolServers>();
        services.TryAddSingleton<Kazo.Jarvis.Kernel.Tools.Servers.IToolServerConnector>(provider => provider.GetRequiredService<SimulatedToolServers>());
        services.TryAddSingleton<SimulatedVoiceActivity>();
        services.TryAddSingleton<IVoiceActivityDetection>(provider => provider.GetRequiredService<SimulatedVoiceActivity>());

        services.TryAddSingleton<SimulatedModelInstaller>();
        services.TryAddSingleton<IModelInstaller>(provider => provider.GetRequiredService<SimulatedModelInstaller>());
        services.TryAddSingleton<SimulatedGraphicsCardProbe>();
        services.TryAddSingleton<IGraphicsCardProbe>(provider => provider.GetRequiredService<SimulatedGraphicsCardProbe>());
        return services;
    }
}
