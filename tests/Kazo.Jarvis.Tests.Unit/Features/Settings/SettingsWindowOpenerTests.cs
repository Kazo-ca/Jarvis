using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features;
using Kazo.Jarvis.Features.Settings.Window;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Settings;

/// <summary>The settings window slice (US1.2): the tray menu event opens one window, titled in the assistant language.</summary>
[TestFixture]
public sealed class SettingsWindowOpenerTests
{
    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    [TestCase("en-US", "Kazo.Jarvis settings")]
    [TestCase("fr-CA", "Paramètres de Kazo.Jarvis")]
    public async Task The_tray_event_opens_the_settings_window(string windows, string title)
    {
        var dialogs = new RecordingDialogs();
        using var services = Services(windows, dialogs);

        await services.GetRequiredService<IMessageBus>().PublishAsync(new SettingsWindowRequested());

        Assert.That(dialogs.Opened, Has.Count.EqualTo(1));
        var request = dialogs.Opened[0];
        Assert.Multiple(() =>
        {
            Assert.That(request.Key, Is.EqualTo(SettingsWindowOpener.DialogKey));
            Assert.That(request.Component, Is.EqualTo(typeof(SettingsWindow)));
            Assert.That(request.Title, Is.EqualTo(title));
        });
    }

    [Test]
    public void Every_slice_registered_by_the_features_has_a_settings_page_with_a_translated_title()
    {
        using var services = Services("en-US", new RecordingDialogs());
        var localizers = services.GetRequiredService<IStringLocalizerFactory>();

        var pages = services.GetServices<SettingsPage>().ToArray();

        Assert.That(pages, Is.Not.Empty);
        Assert.That(
            pages.Where(page => localizers.Create(page.TitleResource)[page.TitleKey].ResourceNotFound).Select(page => page.Key),
            Is.Empty);
    }

    private ServiceProvider Services(string windows, IDialogService dialogs) => new ServiceCollection()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
        .AddSingleton<ISettingsStore>(new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System))
        .AddSingleton<ISystemLanguage>(new FixedSystemLanguage(windows))
        .AddSingleton(dialogs)
        .AddSingleton<IGlobalHotkey, Desktop.FakeGlobalHotkey>()
        .AddSingleton<ILaunchAtLogin, Desktop.FakeLaunchAtLogin>()
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
