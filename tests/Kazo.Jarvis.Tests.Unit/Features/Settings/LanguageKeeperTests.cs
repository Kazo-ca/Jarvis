using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Settings.Language;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Localization;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Settings;

/// <summary>The language slice (US1.4, ADR 0008): proposal from Windows, saved choice, change without restart.</summary>
[TestFixture]
public sealed class LanguageKeeperTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private ListLogger log = null!;
    private readonly List<LanguageKeeper> keepers = [];

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        log = new ListLogger();
        provider = new ServiceCollection().AddSingleton<ILoggerFactory>(log).AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
    }

    [TearDown]
    public void TearDown()
    {
        keepers.ForEach(keeper => keeper.Dispose());
        keepers.Clear();
        log.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [TestCase("fr-CA", "fr")]
    [TestCase("en-GB", "en")]
    [TestCase("es-ES", "en")]
    public void Without_a_choice_the_language_of_Windows_is_used(string windows, string expected)
    {
        var keeper = Create(windows);

        Assert.Multiple(() =>
        {
            Assert.That(keeper.Current.Code, Is.EqualTo(expected));
            Assert.That(keeper.Proposed.Code, Is.EqualTo(expected));
            Assert.That(keeper.IsChosen, Is.False);
        });
    }

    [Test]
    public async Task A_saved_choice_wins_over_Windows()
    {
        await settings.WriteAsync(LanguageOptions.Section, new LanguageOptions { Code = "en" });

        var keeper = Create("fr-FR");

        Assert.Multiple(() =>
        {
            Assert.That(keeper.Current, Is.EqualTo(AssistantLanguage.English));
            Assert.That(keeper.IsChosen, Is.True);
        });
    }

    [Test]
    public async Task A_new_choice_applies_at_once_and_is_announced()
    {
        var keeper = Create("en-US");
        var announced = new TaskCompletionSource<AssistantLanguageChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = provider.GetRequiredService<IMessageBus>().Subscribe<AssistantLanguageChanged>((message, _) =>
        {
            announced.TrySetResult(message);
            return ValueTask.CompletedTask;
        });

        var saved = await keeper.ChooseAsync(AssistantLanguage.French);

        Assert.That(saved.IsSaved, Is.True);
        Assert.That(keeper.Current, Is.EqualTo(AssistantLanguage.French), "Current changes before the event is delivered.");
        var change = await announced.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(change.Previous, Is.EqualTo(AssistantLanguage.English));
            Assert.That(change.Current, Is.EqualTo(AssistantLanguage.French));
            Assert.That(settings.Read(LanguageOptions.Section).Code, Is.EqualTo("fr"));
        });
    }

    [Test]
    public async Task Choosing_the_language_in_use_announces_nothing()
    {
        var keeper = Create("fr-FR");
        var announcements = 0;
        using var subscription = provider.GetRequiredService<IMessageBus>().Subscribe<AssistantLanguageChanged>((_, _) =>
        {
            Interlocked.Increment(ref announcements);
            return ValueTask.CompletedTask;
        });

        await keeper.ChooseAsync(AssistantLanguage.French);
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(announcements, Is.Zero);
            Assert.That(keeper.IsChosen, Is.True);
        });
    }

    [TestCase("de")]
    [TestCase("fr-CA")]
    public async Task An_unsupported_code_is_refused_by_the_store(string code)
    {
        var keeper = Create("en-US");

        var saved = await settings.WriteAsync(LanguageOptions.Section, new LanguageOptions { Code = code });

        Assert.Multiple(() =>
        {
            Assert.That(saved.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(keeper.Current, Is.EqualTo(AssistantLanguage.English));
        });
    }

    [Test]
    public void The_slice_provides_the_language_reader_and_its_settings_page()
    {
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddSingleton(settings)
            .AddSingleton<ISystemLanguage>(new FixedSystemLanguage("fr-FR"))
            .AddJarvisKernel()
            .AddLanguageSlice()
            .BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(services.GetRequiredService<IAssistantLanguageReader>().Current, Is.EqualTo(AssistantLanguage.French));
            Assert.That(services.GetServices<SettingsPage>().Select(page => page.Key), Is.EqualTo(new[] { LanguageSlice.PageKey }));
        });
    }

    private LanguageKeeper Create(string windows)
    {
        var keeper = new LanguageKeeper(
            settings,
            new FixedSystemLanguage(windows),
            provider.GetRequiredService<IMessageBus>(),
            new Logger<LanguageKeeper>(log));
        keepers.Add(keeper);
        return keeper;
    }
}
