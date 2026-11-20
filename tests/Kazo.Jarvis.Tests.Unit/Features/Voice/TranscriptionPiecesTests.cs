using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>The energy fallback, the cleaning of Whisper's text and the settings of the transcription.</summary>
[TestFixture]
public sealed class TranscriptionPiecesTests
{
    [Test]
    public void The_energy_detector_hears_a_voice_above_the_background_noise()
    {
        using var detector = new EnergyVoiceActivityDetector();
        var hum = Tone(0.003f);
        var voice = Tone(0.2f);

        for (var i = 0; i < 20; i++)
        {
            Assert.That(detector.IsSpeech(hum), Is.False);
        }

        Assert.Multiple(() =>
        {
            Assert.That(detector.IsSpeech(voice), Is.True);
            Assert.That(detector.IsSpeech(hum), Is.False);
            Assert.That(detector.NoiseFloor, Is.LessThan(-40f));
        });
    }

    [Test]
    public void The_energy_detector_learns_a_louder_hum_and_ignores_digital_silence()
    {
        using var detector = new EnergyVoiceActivityDetector();
        var silence = new float[EnergyVoiceActivityDetector.DefaultFrameLength];
        Assert.That(detector.IsSpeech(silence), Is.False);

        // A fan starts: heard as speech for a moment, then learned as noise.
        var fan = Tone(0.02f);
        var heard = Enumerable.Range(0, 400).Select(_ => detector.IsSpeech(fan)).ToList();

        Assert.That(heard[^1], Is.False);
    }

    [Test]
    public void Whisper_markers_and_segments_without_speech_are_dropped()
    {
        TranscriptSegment[] segments =
        [
            new(" [BLANK_AUDIO]", 0.1f),
            new(" Quelle heure", 0.05f),
            new("  est-il ? (musique)", 0.05f),
            new(" Sous-titres réalisés par la communauté d'Amara.org", 0.95f),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(TranscriptText.From(segments), Is.EqualTo("Quelle heure est-il ?"));
            Assert.That(TranscriptText.From([new(" [Music]", 0.2f)]), Is.Empty);
            Assert.That(TranscriptText.From([]), Is.Empty);
        });
    }

    [Test]
    public void The_defaults_follow_ADR_0006()
    {
        var options = TranscriptionOptions.Section.CreateDefault();

        Assert.Multiple(() =>
        {
            Assert.That(options.Model, Is.EqualTo(WhisperModel.LargeV3Turbo));
            Assert.That(options.Device, Is.EqualTo(ComputeDevice.Gpu));
            Assert.That(options.Detector, Is.EqualTo(EndOfSpeechDetector.Silero));
            Assert.That(options.SilenceMilliseconds, Is.EqualTo(500));
            Assert.That(TranscriptionOptions.Section.Validate(options), Is.Empty);
        });
    }

    [Test]
    public void Values_out_of_range_are_refused()
    {
        var wrong = new TranscriptionOptions
        {
            Model = (WhisperModel)42,
            SilenceMilliseconds = 50,
            NoSpeechTimeoutSeconds = 0,
            MaxUtteranceSeconds = 40,
        };

        Assert.That(TranscriptionOptions.Section.Validate(wrong), Has.Count.EqualTo(4));
    }

    private static float[] Tone(float amplitude) =>
        [.. Enumerable.Range(0, EnergyVoiceActivityDetector.DefaultFrameLength).Select(i => amplitude * MathF.Sin(2 * MathF.PI * 220 * i / 16_000))];
}
