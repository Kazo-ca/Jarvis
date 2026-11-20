using Kazo.Jarvis.Adapters.Speech;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Speech;

/// <summary>
/// Which local voices are listed (ADR 0006, section 5): only those whose model files are in the models folder.
/// The synthesis itself needs the real models (60 to 330 MB), which are not in the repository; it is checked by
/// hand (doc/fr/dev/speech.md).
/// </summary>
[TestFixture]
public sealed class LocalSpeechEnginesTests
{
    private TemporaryFolder folder = null!;

    private string Models => folder.Paths.Models;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        Directory.CreateDirectory(Models);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [TestCase("fr_FR-tom-medium", "Tom (fr_FR, medium)", "fr")]
    [TestCase("en_GB-alan-medium", "Alan (en_GB, medium)", "en")]
    [TestCase("en_US-libritts_r-medium", "Libritts R (en_US, medium)", "en")]
    [TestCase("de_DE-thorsten", "Thorsten (de_DE)", "de")]
    public void A_Piper_voice_is_described_from_its_name(string id, string name, string language)
    {
        Assert.That(PiperSpeechEngine.Describe(id), Is.EqualTo(new SpeechVoice("piper", id, name, language, LeavesMachine: false)));
    }

    [TestCase("model")]
    [TestCase("x-tom")]
    public void A_name_outside_the_Piper_pattern_is_not_a_voice(string id)
    {
        Assert.That(PiperSpeechEngine.Describe(id), Is.Null);
    }

    [Test]
    public async Task Only_complete_Piper_folders_are_listed()
    {
        InstallPiper("fr_FR-tom-medium");
        InstallPiper("en_GB-alan-medium", withEspeak: false);
        Directory.CreateDirectory(Path.Combine(Models, "vits-piper-empty"));
        using var engine = new PiperSpeechEngine(Models);

        var voices = await engine.ListVoicesAsync();

        Assert.That(voices.Select(voice => voice.Id), Is.EqualTo(new[] { "fr_FR-tom-medium" }));
    }

    [Test]
    public async Task No_models_folder_lists_nothing_and_a_missing_voice_is_named()
    {
        using var engine = new PiperSpeechEngine(Path.Combine(Models, "absent"));
        var voice = new SpeechVoice("piper", "fr_FR-tom-medium", "Tom", "fr", false);

        Assert.That(await engine.ListVoicesAsync(), Is.Empty);
        var failure = Assert.ThrowsAsync<SpeechException>(async () => await engine.SynthesizeAsync(voice, "Bonsoir.", 1));
        Assert.That(failure!.Failure, Is.EqualTo(SpeechFailure.ModelMissing));
    }

    [Test]
    public async Task Kokoro_lists_its_speakers_once_installed()
    {
        using var engine = new KokoroSpeechEngine(Models);
        Assert.That(await engine.ListVoicesAsync(), Is.Empty);

        var kokoro = Directory.CreateDirectory(Path.Combine(Models, KokoroSpeechEngine.FolderName)).FullName;
        foreach (var file in new[] { "model.onnx", "voices.bin", "tokens.txt" })
        {
            await File.WriteAllTextAsync(Path.Combine(kokoro, file), "x");
        }

        Directory.CreateDirectory(Path.Combine(kokoro, "espeak-ng-data"));
        var voices = await engine.ListVoicesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(voices.Single(voice => voice.Language == "fr").Id, Is.EqualTo("ff_siwis"));
            Assert.That(voices.Where(voice => voice.Language == "en").Select(voice => voice.Id), Does.Contain("bm_george"));
            Assert.That(voices, Has.All.Matches<SpeechVoice>(voice => voice.Engine == "kokoro" && !voice.LeavesMachine));
        });
    }

    [Test]
    public void Kokoro_does_not_know_a_foreign_speaker()
    {
        using var engine = new KokoroSpeechEngine(Models);

        var failure = Assert.ThrowsAsync<SpeechException>(async () =>
            await engine.SynthesizeAsync(new SpeechVoice("kokoro", "zz_nobody", "?", "fr", false), "Bonjour.", 1));

        Assert.That(failure!.Failure, Is.EqualTo(SpeechFailure.UnknownVoice));
    }

    private void InstallPiper(string voice, bool withEspeak = true)
    {
        var target = Directory.CreateDirectory(Path.Combine(Models, "vits-piper-" + voice)).FullName;
        File.WriteAllText(Path.Combine(target, voice + ".onnx"), "x");
        File.WriteAllText(Path.Combine(target, "tokens.txt"), "x");
        if (withEspeak)
        {
            Directory.CreateDirectory(Path.Combine(target, "espeak-ng-data"));
        }
    }
}
