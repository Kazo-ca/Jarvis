using Kazo.Jarvis.Adapters;
using Kazo.Jarvis.Adapters.Voice;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Voice;

/// <summary>
/// Whisper.net and Silero adapters (ADR 0006, sections 3 and 4): where the models are looked for, and the cause
/// named when one is missing. No model is in the repository, so the native libraries are not run here; the
/// real run is a Windows check.
/// </summary>
[TestFixture]
public sealed class VoiceAdaptersTests
{
    private TemporaryFolder folder = null!;

    [SetUp]
    public void SetUp() => folder = new TemporaryFolder();

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void The_models_are_looked_for_in_the_models_folder()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VoiceModelFiles.WhisperPath(folder.Paths, WhisperModel.LargeV3Turbo), Is.EqualTo(Path.Combine(folder.Paths.Models, "whisper", "ggml-large-v3-turbo-q5_0.bin")));
            Assert.That(VoiceModelFiles.FileNameOf(WhisperModel.Small), Is.EqualTo("ggml-small.bin"));
            Assert.That(VoiceModelFiles.FileNameOf(WhisperModel.Base), Is.EqualTo("ggml-base.bin"));
            Assert.That(VoiceModelFiles.SileroPath(folder.Paths), Is.EqualTo(Path.Combine(folder.Paths.Models, "vad", "silero_vad.onnx")));
        });
    }

    [Test]
    public void A_missing_Whisper_model_names_the_file_expected()
    {
        var recognition = new WhisperSpeechRecognition(folder.Paths);

        var failure = Assert.ThrowsAsync<SpeechRecognitionException>(async () =>
            await recognition.LoadAsync(new RecognizerSpec(WhisperModel.Base, ComputeDevice.Cpu)));

        Assert.Multiple(() =>
        {
            Assert.That(failure!.Failure, Is.EqualTo(SpeechRecognitionFailure.ModelMissing));
            Assert.That(failure.ModelPath, Is.EqualTo(VoiceModelFiles.WhisperPath(folder.Paths, WhisperModel.Base)));
        });
    }

    [Test]
    public void A_missing_Silero_model_names_the_file_expected()
    {
        var detection = new SileroVoiceActivityDetection(folder.Paths);

        var failure = Assert.Throws<SpeechRecognitionException>(() => detection.Create());

        Assert.Multiple(() =>
        {
            Assert.That(failure!.Failure, Is.EqualTo(SpeechRecognitionFailure.ModelMissing));
            Assert.That(failure.ModelPath, Is.EqualTo(VoiceModelFiles.SileroPath(folder.Paths)));
        });
    }

    [Test]
    public void The_Windows_host_gets_Whisper_and_Silero()
    {
        using var provider = new ServiceCollection().AddJarvisAdapters(folder.Paths).BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<ISpeechRecognition>(), Is.TypeOf<WhisperSpeechRecognition>());
            Assert.That(provider.GetRequiredService<IVoiceActivityDetection>(), Is.TypeOf<SileroVoiceActivityDetection>());
        });
    }
}
