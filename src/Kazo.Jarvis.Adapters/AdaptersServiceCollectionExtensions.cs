using System.Runtime.Versioning;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Hardware;
using Kazo.Jarvis.Adapters.Llm;
using Kazo.Jarvis.Adapters.Models;
using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Speech;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Adapters.Updates;
using Kazo.Jarvis.Adapters.Voice;
using Kazo.Jarvis.Adapters.Web;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Models;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Updates;
using Kazo.Jarvis.Kernel.Voice;
using Kazo.Jarvis.Kernel.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters;

/// <summary>Registers the real implementations of the kernel contracts.</summary>
public static class AdaptersServiceCollectionExtensions
{
    /// <summary>
    /// Adds every adapter to <paramref name="services"/>. Only the Windows host calls it, before
    /// <c>AddJarvisFeatures</c>, so that slices find their contracts (ADR 0002, section 6).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="paths">Where the files go; <c>%USERPROFILE%\.kazo\jarvis</c> when omitted (ADR 0004).</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisAdapters(this IServiceCollection services, JarvisPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(paths ?? JarvisPaths.ForCurrentUser());

        services.TryAddSingleton<ISettingsStore>(provider => new JsonFileSettingsStore(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetServices<ISettingsMigration>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<JsonFileSettingsStore>>()));

        services.TryAddSingleton<IDocumentStore>(provider => new SqliteDocumentStore(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<SqliteDocumentStore>>()));

        // Releases of Kazo-ca/Jarvis on GitHub (US1.18). Only called on the user's request or when the
        // automatic check is on. Its own client: the timeout covers the API call, not the package download.
        services.TryAddSingleton(provider => new GitHubReleaseSource(
            new HttpClient { Timeout = TimeSpan.FromMinutes(10) },
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetService<ILogger<GitHubReleaseSource>>()));
        services.TryAddSingleton<IReleaseSource>(provider => provider.GetRequiredService<GitHubReleaseSource>());

        // Language models (ADR 0007): one client for every provider. Its timeout bounds the wait for the first
        // byte, which includes loading a 9 GB model (70 to 115 s in spike E4); the stream itself is not bounded.
        services.TryAddSingleton<ILlmConnector>(provider => new HttpLlmConnector(
            new HttpClient { Timeout = TimeSpan.FromMinutes(5) },
            provider.GetService<ISecretVault>(),
            provider.GetService<ILogger<HttpLlmConnector>>()));

        // Wake word (ADR 0006, section 2): openWakeWord models in models\wake-word, loaded by the wake word slice.
        services.TryAddSingleton<IWakeWordEngine>(provider => new OnnxWakeWordEngine(provider.GetRequiredService<JarvisPaths>()));
        // Transcription (ADR 0006, sections 3 and 4): Whisper.net and Silero, models read from the models folder.
        services.TryAddSingleton<ISpeechRecognition>(provider => new WhisperSpeechRecognition(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetService<ILogger<WhisperSpeechRecognition>>()));
        services.TryAddSingleton<IVoiceActivityDetection>(provider => new SileroVoiceActivityDetection(
            provider.GetRequiredService<JarvisPaths>()));
        // Voices (ADR 0006, section 5): Piper and Kokoro read the models folder; the cloud voices are listed only
        // when their key is in the vault. One client for both cloud services: 30 s bounds one sentence.
        services.TryAddSingleton(provider => new PiperSpeechEngine(provider.GetRequiredService<JarvisPaths>().Models));
        services.TryAddSingleton(provider => new KokoroSpeechEngine(provider.GetRequiredService<JarvisPaths>().Models));
        services.TryAddSingleton(new SpeechHttp(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));
        services.TryAddSingleton(provider => new ElevenLabsSpeechEngine(
            provider.GetRequiredService<SpeechHttp>().Client,
            provider.GetService<ISecretVault>(),
            provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton(provider => new OpenAiSpeechEngine(
            provider.GetRequiredService<SpeechHttp>().Client,
            provider.GetService<ISecretVault>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, PiperSpeechEngine>(provider => provider.GetRequiredService<PiperSpeechEngine>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, KokoroSpeechEngine>(provider => provider.GetRequiredService<KokoroSpeechEngine>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, ElevenLabsSpeechEngine>(provider => provider.GetRequiredService<ElevenLabsSpeechEngine>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISpeechEngine, OpenAiSpeechEngine>(provider => provider.GetRequiredService<OpenAiSpeechEngine>()));

        // Models (ADR 0006, section 7, stage1/14-first-run): downloaded on the user's request only, checked by
        // checksum. Its own client: 30 minutes cover the largest model (Kokoro, 350 MB) on a slow line.
        services.TryAddSingleton<IModelInstaller>(provider => new HttpModelInstaller(
            new HttpClient { Timeout = TimeSpan.FromMinutes(30) },
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetService<ILogger<HttpModelInstaller>>()));
        services.TryAddSingleton<IGraphicsCardProbe>(provider => new NvidiaSmiGraphicsCardProbe(
            provider.GetService<ILogger<NvidiaSmiGraphicsCardProbe>>()));

        // Basic tools (ADR 0017, section 3): the disk, read only, for the file tools; Brave Search for the web search,
        // called only when the tool is on and a key is in the vault. 20 s bound one search.
        services.TryAddSingleton<ILocalFiles, LocalFiles>();

        // Claude Code sessions (ADR 0021, section 6): the registry of the running sessions, read only.
        services.TryAddSingleton<Kazo.Jarvis.Kernel.ClaudeCode.IClaudeSessionRegistry>(provider =>
            new Kazo.Jarvis.Adapters.ClaudeCode.ClaudeSessionRegistry(provider.GetService<ILogger<Kazo.Jarvis.Adapters.ClaudeCode.ClaudeSessionRegistry>>()));

        // The user's Claude Code settings (ADR 0021, section 6): read to show, written on the user's click only.
        services.TryAddSingleton<Kazo.Jarvis.Kernel.ClaudeCode.IClaudeUserSettings>(_ => new Kazo.Jarvis.Adapters.ClaudeCode.ClaudeUserSettings());

        // Local server (ADR 0021, section 3): 127.0.0.1 only, a token on every route.
        services.TryAddSingleton(provider => new Kazo.Jarvis.Adapters.LocalServer.KestrelLocalServer(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetServices<Kazo.Jarvis.Kernel.LocalServer.ILocalRoute>(),
            provider.GetService<ISecretVault>(),
            provider.GetService<ILogger<Kazo.Jarvis.Adapters.LocalServer.KestrelLocalServer>>(),
            provider.GetService<Kazo.Jarvis.Kernel.ClaudeCode.IClaudeApprovals>()));
        services.TryAddSingleton<Kazo.Jarvis.Kernel.LocalServer.ILocalServer>(provider => provider.GetRequiredService<Kazo.Jarvis.Adapters.LocalServer.KestrelLocalServer>());
        services.AddHostedService(provider => provider.GetRequiredService<Kazo.Jarvis.Adapters.LocalServer.KestrelLocalServer>());

        // Claude Code as a program Jarvis starts (ADR 0021, section 1).
        services.TryAddSingleton<Kazo.Jarvis.Kernel.ClaudeCode.IClaudeCode>(provider => new Kazo.Jarvis.Adapters.ClaudeCode.ClaudeCodeProcess(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetRequiredService<Kazo.Jarvis.Kernel.LocalServer.ILocalServer>(),
            provider.GetService<ILogger<Kazo.Jarvis.Adapters.ClaudeCode.ClaudeCodeProcess>>()));

        // Tool servers (ADR 0018): the MCP client.
        services.TryAddSingleton<Kazo.Jarvis.Kernel.Tools.Servers.IToolServerConnector, Kazo.Jarvis.Adapters.Mcp.McpToolServerConnector>();
        services.TryAddSingleton<IWebSearchApi>(provider => new BraveSearchApi(
            new HttpClient { Timeout = TimeSpan.FromSeconds(20) },
            provider.GetService<ISecretVault>(),
            provider.GetService<ILogger<BraveSearchApi>>()));

        // Both vaults need Windows (Credential Manager, DPAPI). The Windows host is the only one to load Adapters.
        if (OperatingSystem.IsWindows())
        {
            AddWindowsVaults(services);
        }

        return services;
    }

    /// <summary>The selected vault over the Credential Manager and the DPAPI file.</summary>
    [SupportedOSPlatform("windows")]
    private static void AddWindowsVaults(IServiceCollection services)
    {
        services.TryAddSingleton(provider => new SelectedSecretVault(
            provider.GetRequiredService<ISettingsStore>(),
            new Dictionary<VaultKind, ISecretVault>
            {
                [VaultKind.CredentialManager] = new CredentialManagerVault(),
                [VaultKind.EncryptedFile] = new EncryptedFileVault(
                    provider.GetRequiredService<JarvisPaths>().SecretsFile, new DpapiSecretProtector()),
            },
            provider.GetService<ILogger<SelectedSecretVault>>()));
        services.TryAddSingleton<ISecretVault>(provider => provider.GetRequiredService<SelectedSecretVault>());
        services.TryAddSingleton<ISecretVaultSelector>(provider => provider.GetRequiredService<SelectedSecretVault>());
    }
}
