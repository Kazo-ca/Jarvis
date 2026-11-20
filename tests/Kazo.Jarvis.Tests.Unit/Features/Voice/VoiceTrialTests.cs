using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Features.Voice.TestVoice;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>The voices page (US1.11): voices by language, listening, speed, default voice of each language.</summary>
[TestFixture]
public sealed class VoiceTrialTests
{
    private TemporaryFolder folder = null!;
    private FakeSpeechEngine piper = null!;
    private FakeSpeechEngine cloud = null!;
    private FakePlayback playback = null!;
    private VoiceSelection selection = null!;
    private VoiceTrial trial = null!;
    private SpeechVoice tom = null!;
    private SpeechVoice siwis = null!;
    private SpeechVoice alan = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        piper = new FakeSpeechEngine("piper");
        tom = piper.Add("fr_FR-tom-medium", "fr");
        siwis = piper.Add("fr_FR-siwis-medium", "fr");
        alan = piper.Add("en_GB-alan-medium", "en");
        cloud = new FakeSpeechEngine("openai", cloud: true);
        playback = new FakePlayback();
        selection = new VoiceSelection([piper, cloud], new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System));
        trial = new VoiceTrial(selection, [piper, cloud], playback, new ManualLanguage { Current = AssistantLanguage.French });
        await trial.LoadAsync();
    }

    [TearDown]
    public void TearDown()
    {
        trial.Dispose();
        selection.Dispose();
        folder.Dispose();
    }

    [Test]
    public void The_page_opens_on_the_language_of_the_assistant_and_its_saved_voice()
    {
        Assert.Multiple(() =>
        {
            Assert.That(trial.Language, Is.EqualTo(AssistantLanguage.French));
            Assert.That(trial.Voices, Is.EqualTo(new[] { tom, siwis }));
            Assert.That(trial.Selected, Is.EqualTo(tom));
            Assert.That(trial.Speed, Is.EqualTo(1.0));
            Assert.That(trial.IsDefault, Is.True);
        });
    }

    [Test]
    public async Task Listening_says_the_sentence_with_the_voice_and_speed_tried()
    {
        trial.Select(VoiceTrial.KeyOf(siwis));
        trial.ChangeSpeed(1.5);

        await trial.ListenAsync("  Bonsoir.  ");

        Assert.Multiple(() =>
        {
            Assert.That(piper.Said, Is.EqualTo(new[] { (siwis, "Bonsoir.", 1.5) }));
            Assert.That(playback.Played, Has.Count.EqualTo(1));
            Assert.That(trial.IsPlaying, Is.False);
            Assert.That(trial.IsDefault, Is.False);
        });
    }

    [Test]
    public async Task Stop_cuts_only_the_sentence_of_the_page()
    {
        playback.Hold = true;
        var listening = trial.ListenAsync("Bonsoir.");
        await playback.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(trial.IsPlaying, Is.True);

        trial.Stop();
        await listening.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(trial.IsPlaying, Is.False);
            Assert.That(trial.Problem, Is.Null);
            Assert.That(playback.StopAllCount, Is.Zero);
        });
    }

    [Test]
    public async Task The_voice_tried_becomes_the_default_of_its_language_only()
    {
        trial.Select(VoiceTrial.KeyOf(siwis));
        trial.ChangeSpeed(0.8);

        var saved = await trial.MakeDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(trial.IsDefault, Is.True);
            Assert.That(selection.ChoiceFor(AssistantLanguage.French), Is.EqualTo(new VoiceChoice("piper", siwis.Id, 0.8)));
            Assert.That(selection.ChoiceFor(AssistantLanguage.English), Is.EqualTo(SpeechDefaults.For(AssistantLanguage.English)));
        });
    }

    [Test]
    public void Another_language_shows_its_own_voices_and_choice()
    {
        trial.ShowLanguage(AssistantLanguage.English);

        Assert.Multiple(() =>
        {
            Assert.That(trial.Voices, Is.EqualTo(new[] { alan }));
            Assert.That(trial.Selected, Is.EqualTo(alan));
        });
    }

    [Test]
    public async Task A_cloud_voice_appears_after_a_reload_for_every_language()
    {
        var nova = cloud.Add("nova", null);

        await trial.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(trial.Voices, Is.EqualTo(new[] { tom, siwis, nova }));
            Assert.That(trial.Selected, Is.EqualTo(tom));
        });
    }

    [TestCase(SpeechFailure.ModelMissing, TrialProblem.ModelMissing)]
    [TestCase(SpeechFailure.KeyMissing, TrialProblem.KeyMissing)]
    [TestCase(SpeechFailure.KeyRefused, TrialProblem.KeyRefused)]
    [TestCase(SpeechFailure.Unreachable, TrialProblem.Unreachable)]
    [TestCase(SpeechFailure.Failed, TrialProblem.Failed)]
    public async Task A_failure_is_named(SpeechFailure failure, TrialProblem problem)
    {
        piper.FailOn = "Bonsoir";
        piper.Failure = failure;

        await trial.ListenAsync("Bonsoir.");

        Assert.That(trial.Problem, Is.EqualTo(problem));
    }

    [Test]
    public async Task No_output_is_named()
    {
        playback.Unavailable = true;

        await trial.ListenAsync("Bonsoir.");

        Assert.That(trial.Problem, Is.EqualTo(TrialProblem.NoOutput));
    }
}
