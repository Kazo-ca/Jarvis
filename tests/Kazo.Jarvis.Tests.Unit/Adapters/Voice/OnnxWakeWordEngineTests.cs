using Kazo.Jarvis.Adapters.Voice;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Voice;

/// <summary>
/// The real wake word engine (ADR 0006, section 2) on the openWakeWord 0.5.1 models. The models are not in the
/// repository (ADR 0004, section 5): the CI downloads them and names their folder in
/// <c>KAZO_JARVIS_WAKE_WORD_MODELS</c> (see doc/en/dev/build.md).
/// </summary>
[TestFixture]
[Category("Models")]
public sealed class OnnxWakeWordEngineTests
{
    private const string ModelsVariable = "KAZO_JARVIS_WAKE_WORD_MODELS";
    private const string ClipsVariable = "KAZO_JARVIS_WAKE_WORD_CLIPS";

    [Test]
    public void Missing_files_are_named()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllBytes(Path.Combine(folder.Path, OnnxWakeWordEngine.MelFile), [1]);

        var exception = Assert.Throws<WakeWordModelMissingException>(() => new OnnxWakeWordEngine(folder.Path).Load());

        Assert.That(exception!.MissingFiles.Select(Path.GetFileName), Is.EqualTo(new[] { OnnxWakeWordEngine.EmbeddingFile, OnnxWakeWordEngine.ClassifierFile }));
    }

    [Test]
    public void The_engine_looks_in_the_wake_word_folder_of_the_models()
    {
        using var folder = new TemporaryFolder();

        Assert.That(new OnnxWakeWordEngine(folder.Paths).Folder, Is.EqualTo(Path.Combine(folder.Paths.Models, "wake-word")));
    }

    [Test]
    public void Silence_noise_and_a_tone_score_low()
    {
        using var detector = Load();
        var random = new Random(7);
        var clips = new Dictionary<string, float[]>
        {
            ["silence"] = new float[3 * 16_000],
            ["noise"] = [.. Enumerable.Range(0, 3 * 16_000).Select(_ => (float)((random.NextDouble() * 0.2) - 0.1))],
            ["tone"] = [.. Enumerable.Range(0, 3 * 16_000).Select(i => (float)(0.3 * Math.Sin(2 * Math.PI * 440 * i / 16_000)))],
        };

        foreach (var (name, clip) in clips)
        {
            detector.Reset();
            Assert.That(Scores(detector, clip).Max(), Is.LessThan(0.2f), name);
        }
    }

    [Test]
    public void The_first_frames_score_zero_and_reset_gives_the_same_scores_again()
    {
        using var detector = Load();
        var random = new Random(3);
        var clip = Enumerable.Range(0, 2 * 16_000).Select(_ => (float)((random.NextDouble() * 0.6) - 0.3)).ToArray();

        var first = Scores(detector, clip);
        detector.Reset();
        var second = Scores(detector, clip);

        Assert.Multiple(() =>
        {
            Assert.That(detector.FrameLength, Is.EqualTo(1_280));
            Assert.That(first.Take(5), Is.All.Zero);
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public void A_frame_of_the_wrong_length_is_refused()
    {
        using var detector = Load();

        Assert.Throws<ArgumentException>(() => detector.Process(new float[100]));
    }

    /// <summary>
    /// Run by hand with a folder of 16 kHz mono int16 clips: <c>hey-jarvis.pcm</c> must pass 0.5,
    /// <c>hey-travis.pcm</c> must not. Not in the CI: the clips are synthetic voices whose licence does not
    /// allow keeping them in the repository.
    /// </summary>
    [Test]
    [Explicit("Needs clips outside the repository, named by KAZO_JARVIS_WAKE_WORD_CLIPS.")]
    public void A_spoken_hey_jarvis_passes_the_default_threshold_and_a_neighbour_does_not()
    {
        var folder = Environment.GetEnvironmentVariable(ClipsVariable) ?? throw new InconclusiveException($"{ClipsVariable} is not set.");
        using var detector = Load();

        var heyJarvis = Scores(detector, ReadPcm(Path.Combine(folder, "hey-jarvis.pcm"))).Max();
        detector.Reset();
        var heyTravis = Scores(detector, ReadPcm(Path.Combine(folder, "hey-travis.pcm"))).Max();

        TestContext.Out.WriteLine($"hey jarvis {heyJarvis:0.000}, hey travis {heyTravis:0.000}");
        Assert.Multiple(() =>
        {
            Assert.That(heyJarvis, Is.GreaterThan(0.5f));
            Assert.That(heyTravis, Is.LessThan(0.5f));
        });
    }

    private static IWakeWordDetector Load()
    {
        var folder = Environment.GetEnvironmentVariable(ModelsVariable);
        if (string.IsNullOrEmpty(folder))
        {
            Assert.Ignore($"{ModelsVariable} is not set: the openWakeWord models are not available here.");
        }

        return new OnnxWakeWordEngine(folder!).Load();
    }

    private static List<float> Scores(IWakeWordDetector detector, float[] clip)
    {
        var scores = new List<float>();
        for (var start = 0; start + detector.FrameLength <= clip.Length; start += detector.FrameLength)
        {
            scores.Add(detector.Process(clip.AsSpan(start, detector.FrameLength)));
        }

        return scores;
    }

    private static float[] ReadPcm(string file)
    {
        var bytes = File.ReadAllBytes(file);
        var samples = new float[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32_768f;
        }

        return samples;
    }
}
