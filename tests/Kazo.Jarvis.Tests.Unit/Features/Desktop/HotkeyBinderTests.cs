using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Desktop.Hotkey;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Features.Desktop;

/// <summary>The hotkey slice (US1.6): registration, change of setting, activation.</summary>
[TestFixture]
public sealed class HotkeyBinderTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private FakeGlobalHotkey hotkey = null!;
    private ListLogger log = null!;
    private HotkeyBinder binder = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        log = new ListLogger();
        provider = new ServiceCollection().AddSingleton<ILoggerFactory>(log).AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        hotkey = new FakeGlobalHotkey();
        binder = new HotkeyBinder(hotkey, settings, provider.GetRequiredService<IMessageBus>(), new Logger<HotkeyBinder>(log));
    }

    [TearDown]
    public void TearDown()
    {
        binder.Dispose();
        log.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_default_hotkey_is_registered_at_start()
    {
        await binder.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(hotkey.Registered, Is.EqualTo(new[] { HotkeyGesture.Parse(HotkeyOptions.DefaultGesture) }));
            Assert.That(binder.LastResult, Is.EqualTo(HotkeyRegistration.Registered));
        });
    }

    [Test]
    public async Task A_press_asks_the_state_machine_for_an_activation_by_hotkey()
    {
        var received = new TaskCompletionSource<ActivationRequested>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = provider.GetRequiredService<IMessageBus>().Subscribe<ActivationRequested>((message, _) =>
        {
            received.TrySetResult(message);
            return ValueTask.CompletedTask;
        });
        await binder.StartAsync(CancellationToken.None);

        hotkey.Press();

        var activation = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(activation.Source, Is.EqualTo(ActivationSource.Hotkey));
    }

    [Test]
    public async Task A_changed_setting_registers_the_new_hotkey_without_restart()
    {
        await binder.StartAsync(CancellationToken.None);

        var saved = await settings.WriteAsync(HotkeyOptions.Section, new HotkeyOptions { Gesture = "Win+Shift+F9" });

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsSaved, Is.True);
            Assert.That(hotkey.Current, Is.EqualTo(HotkeyGesture.Parse("Shift+Win+F9")));
            Assert.That(hotkey.Registered, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task An_invalid_setting_is_refused_by_the_store()
    {
        await binder.StartAsync(CancellationToken.None);

        var saved = await settings.WriteAsync(HotkeyOptions.Section, new HotkeyOptions { Gesture = "Shift+J" });

        Assert.Multiple(() =>
        {
            Assert.That(saved.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(hotkey.Registered, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task A_hand_edited_invalid_value_falls_back_to_the_default()
    {
        Directory.CreateDirectory(folder.Paths.Root);
        await File.WriteAllTextAsync(folder.Paths.ConfigFile, """{ "schemaVersion": 1, "Desktop.Hotkey": { "gesture": "J" } }""");
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        binder = new HotkeyBinder(hotkey, settings, provider.GetRequiredService<IMessageBus>(), new Logger<HotkeyBinder>(log));

        await binder.StartAsync(CancellationToken.None);

        Assert.That(hotkey.Current, Is.EqualTo(HotkeyGesture.Parse(HotkeyOptions.DefaultGesture)));
    }

    [Test]
    public async Task A_hotkey_taken_by_another_application_is_logged_as_a_warning()
    {
        hotkey.NextResult = HotkeyRegistration.AlreadyTaken;

        await binder.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(binder.LastResult, Is.EqualTo(HotkeyRegistration.AlreadyTaken));
            Assert.That(log.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message),
                Has.Some.Contains("AlreadyTaken"));
        });
    }

    [Test]
    public async Task Stop_removes_the_hotkey_and_stops_following_the_setting()
    {
        await binder.StartAsync(CancellationToken.None);
        await binder.StopAsync(CancellationToken.None);

        await settings.WriteAsync(HotkeyOptions.Section, new HotkeyOptions { Gesture = "Ctrl+Alt+K" });

        Assert.Multiple(() =>
        {
            Assert.That(hotkey.Unregistrations, Is.EqualTo(1));
            Assert.That(hotkey.Current, Is.Null);
            Assert.That(hotkey.Registered, Has.Count.EqualTo(1));
        });
    }
}
