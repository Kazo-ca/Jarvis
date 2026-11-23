using System.Globalization;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features;
using Kazo.Jarvis.Features.Assistant.Faults;
using Kazo.Jarvis.Features.Settings.Window;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>
/// The faults slice (US1.17, stage1/15-faults): every cause has its text, its corrective action and its setting; the
/// window opens on request and when an activation is refused for want of a microphone; an unusable settings file is
/// raised at start.
/// </summary>
[TestFixture]
public sealed class FaultsTests
{
    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    private static IEnumerable<AssistantFault> EveryFault()
    {
        foreach (var cause in Enum.GetValues<FaultCause>())
        {
            yield return new AssistantFault(cause);
            foreach (var origin in Enum.GetValues<FaultOrigin>())
            {
                yield return new AssistantFault(cause, null, origin);
            }
        }

        foreach (var outcome in Enum.GetValues<SettingsLoadOutcome>())
        {
            yield return new AssistantFault(FaultCause.ConfigurationUnreadable, FaultAdvice.ConfigurationDetail(outcome), FaultOrigin.Settings);
        }
    }

    [Test]
    public void Every_fault_has_its_three_texts_in_both_languages([ValueSource(nameof(EveryFault))] AssistantFault fault, [Values("en-US", "fr-CA")] string windows)
    {
        using var services = Services(windows, new RecordingDialogs());
        var texts = services.GetRequiredService<IStringLocalizer<FaultWindow>>();
        var advice = FaultAdvice.For(fault);

        Assert.That(
            new[] { advice.TitleKey, advice.ExplanationKey, advice.ActionKey }.Where(key => texts[key].ResourceNotFound),
            Is.Empty);
    }

    [Test]
    public void The_fixed_texts_exist_in_both_languages([Values("en-US", "fr-CA")] string windows)
    {
        using var services = Services(windows, new RecordingDialogs());
        var texts = services.GetRequiredService<IStringLocalizer<FaultWindow>>();

        Assert.That(
            new[] { "WindowTitle", "WhatToDo", "OpenSetting", "Detail", "NoFault", "Resolved" }.Where(key => texts[key].ResourceNotFound),
            Is.Empty);
    }

    [Test]
    public void Every_page_named_by_a_fault_is_registered_by_its_slice()
    {
        using var services = Services("en-US", new RecordingDialogs());
        var pages = services.GetServices<SettingsPage>().Select(page => page.Key).ToHashSet();

        var named = EveryFault().Select(fault => FaultAdvice.For(fault).PageKey).OfType<string>().Distinct();

        Assert.That(named.Where(key => !pages.Contains(key)), Is.Empty);
    }

    [TestCase(FaultCause.MicrophoneMissing, null, FaultAdvice.AudioPage)]
    [TestCase(FaultCause.ModelUnreachable, FaultOrigin.LanguageModel, FaultAdvice.LanguageModelsPage)]
    [TestCase(FaultCause.ModelMissing, FaultOrigin.LanguageModel, FaultAdvice.LanguageModelsPage)]
    [TestCase(FaultCause.ModelMissing, FaultOrigin.Transcription, FaultAdvice.VoiceModelsPage)]
    [TestCase(FaultCause.ModelMissing, FaultOrigin.Voice, FaultAdvice.VoiceModelsPage)]
    [TestCase(FaultCause.ModelMissing, FaultOrigin.WakeWord, FaultAdvice.WakeWordPage)]
    [TestCase(FaultCause.VoiceMissing, FaultOrigin.Voice, FaultAdvice.VoicesPage)]
    [TestCase(FaultCause.ConfigurationUnreadable, FaultOrigin.Settings, null)]
    public void Each_cause_links_to_the_setting_that_fixes_it(FaultCause cause, FaultOrigin? origin, string? page) =>
        Assert.That(FaultAdvice.For(new AssistantFault(cause, null, origin)).PageKey, Is.EqualTo(page));

    [TestCase(FaultCause.MicrophoneMissing, FaultOrigin.Microphone)]
    [TestCase(FaultCause.ModelUnreachable, FaultOrigin.LanguageModel)]
    [TestCase(FaultCause.ModelMissing, FaultOrigin.Transcription)]
    [TestCase(FaultCause.VoiceMissing, FaultOrigin.Voice)]
    [TestCase(FaultCause.ConfigurationUnreadable, FaultOrigin.Settings)]
    public void A_fault_without_an_origin_takes_the_usual_one(FaultCause cause, FaultOrigin origin) =>
        Assert.That(FaultAdvice.For(new AssistantFault(cause)).Origin, Is.EqualTo(origin));

    [TestCase(SettingsLoadOutcome.Unreadable, "Configuration_Unreadable")]
    [TestCase(SettingsLoadOutcome.NewerVersion, "Configuration_NewerVersion")]
    [TestCase(SettingsLoadOutcome.MigrationFailed, "Configuration_MigrationFailed")]
    public void An_unusable_settings_file_says_why(SettingsLoadOutcome outcome, string explanation)
    {
        var advice = FaultAdvice.For(new AssistantFault(FaultCause.ConfigurationUnreadable, FaultAdvice.ConfigurationDetail(outcome)));

        Assert.That(advice.ExplanationKey, Is.EqualTo(explanation));
    }

    [TestCase("en-US", "Jarvis problem")]
    [TestCase("fr-CA", "Problème de Jarvis")]
    public async Task The_tray_entry_opens_the_faults_window(string windows, string title)
    {
        var dialogs = new RecordingDialogs();
        using var services = Services(windows, dialogs);

        await services.GetRequiredService<IMessageBus>().PublishAsync(new FaultWindowRequested());

        Assert.That(dialogs.Opened, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(dialogs.Opened[0].Key, Is.EqualTo(FaultWindowOpener.DialogKey));
            Assert.That(dialogs.Opened[0].Component, Is.EqualTo(typeof(FaultWindow)));
            Assert.That(dialogs.Opened[0].Title, Is.EqualTo(title));
        });
    }

    [Test]
    public async Task A_click_refused_for_want_of_a_microphone_opens_the_window_and_a_muted_one_does_not()
    {
        var dialogs = new RecordingDialogs();
        using var services = Services("en-US", dialogs);
        var bus = services.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(new ActivationRefused(ActivationSource.Click, ActivationRefusal.MicrophoneMuted));
        Assert.That(dialogs.Opened, Is.Empty);

        await bus.PublishAsync<AssistantInput>(new FaultRaised(new AssistantFault(FaultCause.MicrophoneMissing, null, FaultOrigin.Microphone)));
        await bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.Hotkey));

        Assert.That(dialogs.Opened.Select(request => request.Key), Is.EqualTo(new[] { FaultWindowOpener.DialogKey }));
    }

    [Test]
    public async Task A_settings_request_with_a_page_selects_that_page()
    {
        var dialogs = new RecordingDialogs();
        using var services = Services("en-US", dialogs);
        var state = services.GetRequiredService<SettingsWindowState>();
        var changes = 0;
        state.Changed += () => changes++;

        await services.GetRequiredService<IMessageBus>().PublishAsync(new SettingsWindowRequested(FaultAdvice.AudioPage));
        await services.GetRequiredService<IMessageBus>().PublishAsync(new SettingsWindowRequested());

        Assert.Multiple(() =>
        {
            Assert.That(state.SelectedKey, Is.EqualTo(FaultAdvice.AudioPage), "a request without a page keeps the page shown");
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(dialogs.Opened, Has.Count.EqualTo(2));
        });
    }

    [TestCase("{ not json", SettingsLoadOutcome.Unreadable)]
    [TestCase("{\"schemaVersion\": 999}", SettingsLoadOutcome.NewerVersion)]
    public async Task An_unusable_settings_file_raises_its_fault_at_start(string content, SettingsLoadOutcome outcome)
    {
        Directory.CreateDirectory(folder.Paths.Root);
        await File.WriteAllTextAsync(folder.Paths.ConfigFile, content);
        using var services = Services("en-US", new RecordingDialogs());

        await services.GetRequiredService<ConfigurationFaultReporter>().StartAsync(CancellationToken.None);

        Assert.That(
            services.GetRequiredService<IAssistantStateReader>().Current.Fault,
            Is.EqualTo(new AssistantFault(FaultCause.ConfigurationUnreadable, outcome.ToString(), FaultOrigin.Settings)));
    }

    [Test]
    public async Task A_usable_settings_file_raises_nothing()
    {
        using var services = Services("en-US", new RecordingDialogs());

        await services.GetRequiredService<ConfigurationFaultReporter>().StartAsync(CancellationToken.None);

        Assert.That(services.GetRequiredService<IAssistantStateReader>().Current.Fault, Is.Null);
    }

    [Test]
    public void The_settings_file_check_runs_at_start()
    {
        // The other hosted services need the host's contracts: only this one is built.
        var hosted = new ServiceCollection().AddJarvisFeatures()
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationFactory is not null)
            .ToArray();
        using var services = Services("en-US", new RecordingDialogs());

        Assert.That(hosted.Select(descriptor => Build(descriptor, services)).OfType<ConfigurationFaultReporter>(), Is.Not.Empty);
    }

    private static object? Build(ServiceDescriptor descriptor, IServiceProvider services)
    {
        try
        {
            return descriptor.ImplementationFactory!(services);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private ServiceProvider Services(string windows, IDialogService dialogs) => new ServiceCollection()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
        .AddSingleton<ISettingsStore>(_ => new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System))
        .AddSingleton<ISystemLanguage>(new FixedSystemLanguage(windows))
        .AddSingleton(dialogs)
        .AddSingleton<IGlobalHotkey, Desktop.FakeGlobalHotkey>()
        .AddSingleton<ILaunchAtLogin, Desktop.FakeLaunchAtLogin>()

        // The agents page comes with a host that has the folder of the agent files.
        .AddSingleton<Kazo.Jarvis.Kernel.Agents.IAgentFiles>(_ => new Kazo.Jarvis.Adapters.Agents.DiskAgentFiles(folder.Paths))
        .AddJarvisFeatures()
        .BuildServiceProvider();

    private sealed class RecordingDialogs : IDialogService
    {
        public List<DialogRequest> Opened { get; } = [];

        // No test here closes a window.
        public event Action<string>? Closed
        {
            add { }
            remove { }
        }

        public void Open(DialogRequest request) => Opened.Add(request);

        public void Close(string key)
        {
        }

        public bool IsOpen(string key) => Opened.Any(request => request.Key == key);
    }
}
