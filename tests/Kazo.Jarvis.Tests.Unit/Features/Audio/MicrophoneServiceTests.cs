using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Audio.Devices;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Messages.Audio;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Features.Audio;

/// <summary>
/// The microphone of the audio slice (US1.13): the right device opens, mute closes it, a lost microphone
/// sets the fault and a new one clears it. Runs with the real kernel, so the state machine is checked too.
/// </summary>
[TestFixture]
public sealed class MicrophoneServiceTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private FakeAudioCapture capture = null!;
    private ListLogger log = null!;
    private MicrophoneService service = null!;
    private List<SoundLevel> levels = null!;
    private IDisposable levelSubscription = null!;

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    private MicrophoneFeed Feed => provider.GetRequiredService<MicrophoneFeed>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        log = new ListLogger();
        provider = new ServiceCollection().AddSingleton<ILoggerFactory>(log).AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        capture = new FakeAudioCapture();
        levels = [];
        levelSubscription = provider.GetRequiredService<ISoundLevelSource>().Subscribe(level =>
        {
            lock (levels)
            {
                levels.Add(level);
            }
        });
        service = CreateService();
    }

    [TearDown]
    public void TearDown()
    {
        levelSubscription.Dispose();
        service.Dispose();
        log.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_Windows_default_microphone_opens_at_start()
    {
        await service.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Opened, Is.EqualTo(new[] { FakeAudioCapture.Desk.Id }));
            Assert.That(service.Status, Is.EqualTo(new MicrophoneStatus(false, FakeAudioCapture.Desk, false)));
            Assert.That(State.Current.Fault, Is.Null);
        });
    }

    [Test]
    public async Task The_chosen_microphone_opens_and_a_new_choice_switches_without_restart()
    {
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { MicrophoneId = FakeAudioCapture.Headset.Id });
        await service.StartAsync(CancellationToken.None);
        Assert.That(capture.Current!.Device, Is.EqualTo(FakeAudioCapture.Headset));
        var first = capture.Current;

        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { MicrophoneId = FakeAudioCapture.Desk.Id });

        await Eventually(() => capture.Current?.Device == FakeAudioCapture.Desk);
        Assert.That(first.Disposed, Is.True);
    }

    [Test]
    public async Task Samples_reach_the_listening_buffer_and_the_level_reaches_the_orb()
    {
        await service.StartAsync(CancellationToken.None);

        capture.Current!.Deliver(Tone(0.5f, 1_600));

        var copy = new float[1_600];
        Assert.Multiple(() =>
        {
            Assert.That(Feed.CopyLatest(copy), Is.EqualTo(1_600));
            Assert.That(copy[400], Is.EqualTo(0.5f));
            Assert.That(LevelsOf(SoundOrigin.Microphone), Has.Some.GreaterThan(0.8f));
        });
    }

    [Test]
    public async Task Mute_closes_the_stream_empties_the_buffer_and_sets_the_condition()
    {
        await service.StartAsync(CancellationToken.None);
        var stream = capture.Current!;
        stream.Deliver(Tone(0.5f, 1_600));

        await service.HandleAsync(new MicrophoneMuteRequested(true), CancellationToken.None);

        await Eventually(() => State.Current.MicrophoneMuted);
        Assert.Multiple(() =>
        {
            Assert.That(stream.Disposed, Is.True);
            Assert.That(Feed.CopyLatest(new float[10]), Is.Zero);
            Assert.That(service.Status, Is.EqualTo(new MicrophoneStatus(true, null, false)));
            Assert.That(State.Current.Look, Is.EqualTo(AssistantLook.MicrophoneMuted));
            Assert.That(LevelsOf(SoundOrigin.Microphone).Last(), Is.Zero);
            Assert.That(settings.Read(AudioDevicesOptions.Section).MicrophoneMuted, Is.True);
        });
    }

    [Test]
    public async Task Unmute_opens_the_microphone_again()
    {
        await service.StartAsync(CancellationToken.None);
        await service.HandleAsync(new MicrophoneMuteRequested(true), CancellationToken.None);
        await Eventually(() => State.Current.MicrophoneMuted);

        await service.HandleAsync(new MicrophoneMuteRequested(false), CancellationToken.None);

        await Eventually(() => !State.Current.MicrophoneMuted && capture.Current is { Disposed: false });
        Assert.That(capture.Opened, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task A_mute_saved_before_a_restart_keeps_the_microphone_closed()
    {
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { MicrophoneMuted = true });

        await service.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Opened, Is.Empty);
            Assert.That(State.Current.MicrophoneMuted, Is.True);
        });
    }

    [Test]
    public async Task An_unplugged_microphone_falls_back_on_another_one()
    {
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { MicrophoneId = FakeAudioCapture.Headset.Id });
        await service.StartAsync(CancellationToken.None);

        capture.Unplug(FakeAudioCapture.Headset);

        await Eventually(() => capture.Current is { Disposed: false } open && open.Device == FakeAudioCapture.Desk);
        Assert.That(State.Current.Fault, Is.Null);

        // Back again: the chosen one is taken again.
        capture.Plug(FakeAudioCapture.Headset);
        await Eventually(() => capture.Current is { Disposed: false } open && open.Device == FakeAudioCapture.Headset);
    }

    [Test]
    public async Task No_microphone_sets_the_fault_and_a_new_one_clears_it()
    {
        await service.StartAsync(CancellationToken.None);

        capture.Unplug(FakeAudioCapture.Headset);
        capture.Unplug(FakeAudioCapture.Desk);

        await Eventually(() => State.Current.Fault?.Cause == FaultCause.MicrophoneMissing);
        Assert.That(service.Status.Missing, Is.True);

        capture.Plug(FakeAudioCapture.Desk);

        await Eventually(() => State.Current.Fault is null && capture.Current is { Disposed: false });
        Assert.That(service.Status, Is.EqualTo(new MicrophoneStatus(false, FakeAudioCapture.Desk, false)));
    }

    [Test]
    public async Task A_microphone_that_refuses_to_open_sets_the_fault_and_is_logged()
    {
        capture.RefuseOpening = true;

        await service.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.MicrophoneMissing));
            Assert.That(log.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message),
                Has.Some.Contains("could not be opened"));
        });
    }

    [Test]
    public async Task Mute_clears_a_missing_microphone_fault()
    {
        capture.Unplug(FakeAudioCapture.Headset);
        capture.Unplug(FakeAudioCapture.Desk);
        await service.StartAsync(CancellationToken.None);
        Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.MicrophoneMissing));

        await service.HandleAsync(new MicrophoneMuteRequested(true), CancellationToken.None);

        await Eventually(() => State.Current.MicrophoneMuted && State.Current.Fault is null);
    }

    [Test]
    public async Task Stop_closes_the_stream_and_stops_following_the_devices()
    {
        await service.StartAsync(CancellationToken.None);
        var stream = capture.Current!;

        await service.StopAsync(CancellationToken.None);
        capture.Unplug(FakeAudioCapture.Desk);
        capture.Plug(FakeAudioCapture.Desk);
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(stream.Disposed, Is.True);
            Assert.That(capture.Opened, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task The_slice_handles_the_mute_request_of_the_tray_menu()
    {
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(log)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddSingleton<IAudioCapture>(capture)
            .AddSingleton<IAudioPlayback>(new FakeAudioPlayback())
            .AddSingleton(settings)
            .AddJarvisKernel()
            .AddAudioDevicesSlice();
        await using var built = services.BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(built.GetServices<IEventHandler<MicrophoneMuteRequested>>().Single(), Is.SameAs(built.GetRequiredService<MicrophoneService>()));
            Assert.That(built.GetServices<SettingsPage>().Select(page => page.Key), Does.Contain(AudioDevicesSlice.PageKey));
        });
    }

    private MicrophoneService CreateService() => new(
        capture,
        settings,
        provider.GetRequiredService<IMessageBus>(),
        provider.GetRequiredService<IMicrophoneSink>(),
        provider.GetRequiredService<ISoundLevelSink>(),
        new Logger<MicrophoneService>(log));

    private float[] LevelsOf(SoundOrigin origin)
    {
        lock (levels)
        {
            return [.. levels.Where(level => level.Origin == origin).Select(level => level.Value)];
        }
    }

    private static float[] Tone(float amplitude, int length) => [.. Enumerable.Repeat(amplitude, length)];

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition did not become true within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }
}
