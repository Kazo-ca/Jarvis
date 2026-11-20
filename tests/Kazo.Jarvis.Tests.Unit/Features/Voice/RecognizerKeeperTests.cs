using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>The Whisper model (ADR 0006, section 4): loaded once, reloaded on a change, looked for again when missing.</summary>
[TestFixture]
public sealed class RecognizerKeeperTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private FakeSpeechRecognition recognition = null!;
    private RecognizerKeeper keeper = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        recognition = new FakeSpeechRecognition();
        keeper = new RecognizerKeeper(recognition, settings, NullLogger<RecognizerKeeper>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        keeper.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_default_model_is_loaded_once_at_start()
    {
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();

        await keeper.TranscribeAsync(new float[16_000], AssistantLanguage.French, CancellationToken.None);
        await keeper.TranscribeAsync(new float[16_000], AssistantLanguage.French, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(recognition.Loads, Is.EqualTo(new[] { new RecognizerSpec(WhisperModel.LargeV3Turbo, ComputeDevice.Gpu) }));
            Assert.That(keeper.Status, Is.EqualTo(new RecognizerStatus(RecognizerState.Ready, new RecognizerSpec(WhisperModel.LargeV3Turbo, ComputeDevice.Gpu), ComputeDevice.Gpu)));
            Assert.That(recognition.Created[0].Transcribed, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task The_status_tells_when_CUDA_was_not_available()
    {
        recognition.ActualDevice = ComputeDevice.Cpu;

        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();

        Assert.That(keeper.Status.Device, Is.EqualTo(ComputeDevice.Cpu));
    }

    [Test]
    public async Task A_change_of_model_frees_the_old_one_and_loads_the_new_one()
    {
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();

        await settings.WriteAsync(TranscriptionOptions.Section, new TranscriptionOptions { Model = WhisperModel.Base, Device = ComputeDevice.Cpu });
        await WaitForAsync(() => keeper.Status.State == RecognizerState.Ready && keeper.Status.Spec.Model == WhisperModel.Base);

        Assert.Multiple(() =>
        {
            Assert.That(recognition.Loads, Has.Length.EqualTo(2));
            Assert.That(recognition.Created[0].Disposed, Is.True);
            Assert.That(recognition.Created[1].Spec, Is.EqualTo(new RecognizerSpec(WhisperModel.Base, ComputeDevice.Cpu)));
        });
    }

    [Test]
    public async Task A_change_of_another_setting_does_not_reload()
    {
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();

        await settings.WriteAsync(TranscriptionOptions.Section, new TranscriptionOptions { SilenceMilliseconds = 800 });
        await keeper.WhenLoadedAsync();

        Assert.That(recognition.Loads, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task A_missing_model_is_shown_then_found_at_the_next_transcription()
    {
        recognition.Missing = true;
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(keeper.Status.State, Is.EqualTo(RecognizerState.Missing));
            Assert.That(keeper.Status.ModelPath, Does.EndWith("LargeV3Turbo.bin"));
        });

        var failure = Assert.ThrowsAsync<SpeechRecognitionException>(() =>
            keeper.TranscribeAsync(new float[100], AssistantLanguage.English, CancellationToken.None));
        Assert.That(failure!.Failure, Is.EqualTo(SpeechRecognitionFailure.ModelMissing));

        // The file is put in place: the next transcription loads it without a restart.
        recognition.Missing = false;
        var segments = await keeper.TranscribeAsync(new float[100], AssistantLanguage.English, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(segments, Is.Not.Empty);
            Assert.That(keeper.Status.State, Is.EqualTo(RecognizerState.Ready));
        });
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "timed out");
            await Task.Delay(10);
        }
    }
}
