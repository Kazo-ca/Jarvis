using Kazo.Jarvis.Features.Assistant.Faults;
using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Features.Audio.Devices;
using Kazo.Jarvis.Features.Desktop.Hotkey;
using Kazo.Jarvis.Features.Desktop.LaunchAtLogin;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Features.Maintenance.Updates;
using Kazo.Jarvis.Features.Permissions.Confirmation;
using Kazo.Jarvis.Features.Permissions.Decision;
using Kazo.Jarvis.Features.Settings.Language;
using Kazo.Jarvis.Features.Settings.Window;
using Kazo.Jarvis.Features.Setup.FirstRun;
using Kazo.Jarvis.Features.Setup.Models;
using Kazo.Jarvis.Features.Voice.WakeWord;
using Kazo.Jarvis.Features.Shared;
using Kazo.Jarvis.Features.Tools.Calculator;
using Kazo.Jarvis.Features.Tools.Catalog;
using Kazo.Jarvis.Features.Tools.Clock;
using Kazo.Jarvis.Features.Tools.Files;
using Kazo.Jarvis.Features.Tools.Web;
using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Features.Voice.TestVoice;
using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Features;

/// <summary>Registers every slice (ADR 0002, section 6).</summary>
public static class FeaturesServiceCollectionExtensions
{
    /// <summary>
    /// Adds every slice to <paramref name="services"/>. Each slice exposes its own registration
    /// method; this one calls them all. Hosts call it after registering the contracts.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The slices talk through the kernel's bus and read its state machine (ADR 0002, section 5).
        services.AddJarvisKernel();

        // Texts in the language of the assistant, for every slice (ADR 0008).
        services.AddJarvisShared();

        // Desktop slices (stage1/4-hosts): their contracts come from the host.
        services.AddHotkeySlice();
        services.AddLaunchAtLoginSlice();

        // Settings slices (stage1/5-shared-ui-language): the language, and the window that shows the pages.
        services.AddLanguageSlice();
        services.AddSettingsWindowSlice();

        // Updates (stage1/16-updates): release source and updater launcher come from the host.
        services.AddUpdatesSlice();

        // The orb (stage1/6-orb): its component reads the kernel state; the click publishes the activation.
        services.AddOrbSlice();

        // Language models (stage1/11-llm-providers): the connector comes from the host; the default model is
        // preloaded in the background and read by the conversation (work item 13).
        services.AddLlmProvidersSlice();

        // Audio devices (stage1/8-audio-devices): capture and playback come from the host.
        services.AddAudioDevicesSlice();

        // Wake word (stage1/9-wake-word): the engine comes from the host, the microphone stream from the kernel.
        services.AddWakeWordSlice();
        // Transcription (stage1/10-transcription): end of speech and Whisper; the recognition and Silero come
        // from the host.
        services.AddTranscriptionSlice();

        // Speech (stage1/12-speech): the engines come from the host; the voices page uses the same engines.
        services.AddSpeechSlice();
        services.AddTestVoiceSlice();

        // Conversation (stage1/13-conversation): the full turn by messages, the interruption, the conversation window
        // and the personality; the default model comes from the language models slice.
        services.AddConversationSlice();

        // First run (stage1/14-first-run): the models page downloads the models (installer and graphics card from the
        // host); the wizard shows the language, audio, models and language model pages one after the other.
        services.AddModelsSlice();
        services.AddFirstRunSlice();

        // Faults (stage1/15-faults): each slice raises and clears its own causes; this one names them, says what to
        // do and opens the right setting, and checks the settings file at start.
        services.AddFaultsSlice();

        // Tools (stage2/3-tool-loop, ADR 0017): every call a model asks for goes through the decision point; each
        // tool slice registers its tool in the registry of the kernel, read by the conversation.
        services.AddPermissionDecisionSlice();

        // Confirmation (stage2/4-confirmation, ADR 0023): the decision point asks the user through it before a
        // "Sensitive" or "Critical" call; the speech slice says the question, the transcription slice hears the answer.
        services.AddConfirmationSlice();
        services.AddClockToolSlice();
        services.AddCalculatorToolSlice();

        // Basic tools (stage2/5-basic-tools, ADR 0017 section 3, ADR 0022): the tools page with one switch per tool;
        // the file tools read the allowed folders only (the disk comes from the host); the web tools search and read
        // a page in the browser of Jarvis (from the host) and are off until the user turns them on.
        services.AddToolCatalogSlice();
        services.AddFileToolsSlice();
        services.AddWebToolsSlice();

        // Other slices arrive with the next work items.
        return services;
    }
}
