using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>Which voice speaks a language (US1.10): the chosen one, else a local one, never an unchosen cloud one.</summary>
[TestFixture]
public sealed class VoiceSelectionTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private FakeSpeechEngine piper = null!;
    private FakeSpeechEngine kokoro = null!;
    private FakeSpeechEngine cloud = null!;
    private VoiceSelection selection = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        piper = new FakeSpeechEngine("piper");
        kokoro = new FakeSpeechEngine("kokoro");
        cloud = new FakeSpeechEngine("openai", cloud: true);

        // The cloud engine is registered first: the selection still lists local voices first.
        selection = new VoiceSelection([cloud, piper, kokoro], settings);
    }

    [TearDown]
    public void TearDown()
    {
        selection.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_default_Piper_voice_speaks_when_nothing_was_chosen()
    {
        var tom = piper.Add("fr_FR-tom-medium", "fr");
        piper.Add("en_GB-alan-medium", "en");

        var resolved = await selection.ResolveAsync(AssistantLanguage.French);

        Assert.That(resolved, Is.EqualTo((tom, 1.0)));
    }

    [Test]
    public async Task The_chosen_voice_and_speed_speak_and_the_change_is_announced()
    {
        piper.Add("fr_FR-tom-medium", "fr");
        var siwis = kokoro.Add("ff_siwis", "fr");
        var changed = 0;
        selection.Changed += (_, _) => changed++;

        var result = await selection.ChooseAsync(AssistantLanguage.French, siwis.ToChoice(1.3));
        var resolved = await selection.ResolveAsync(AssistantLanguage.French);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(resolved, Is.EqualTo((siwis, 1.3)));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(selection.ChoiceFor(AssistantLanguage.English), Is.EqualTo(SpeechDefaults.For(AssistantLanguage.English)));
        });
    }

    [Test]
    public async Task A_chosen_cloud_voice_speaks_every_language()
    {
        var nova = cloud.Add("nova", null);
        await selection.ChooseAsync(AssistantLanguage.English, nova.ToChoice());

        Assert.That((await selection.ResolveAsync(AssistantLanguage.English))?.Voice, Is.EqualTo(nova));
    }

    [Test]
    public async Task A_gone_voice_falls_back_on_a_local_voice_of_the_language_never_on_the_cloud()
    {
        cloud.Add("nova", null);
        var siwis = kokoro.Add("ff_siwis", "fr");
        kokoro.Add("bm_george", "en");
        await selection.ChooseAsync(AssistantLanguage.French, new VoiceChoice("elevenlabs", "removed-voice", 0.8));

        var resolved = await selection.ResolveAsync(AssistantLanguage.French);

        Assert.That(resolved, Is.EqualTo((siwis, 0.8)));
    }

    [Test]
    public async Task No_voice_for_the_language_gives_nothing()
    {
        cloud.Add("nova", null);
        piper.Add("en_GB-alan-medium", "en");

        Assert.That(await selection.ResolveAsync(AssistantLanguage.French), Is.Null);
    }

    [Test]
    public async Task An_engine_that_fails_to_list_hides_only_its_own_voices()
    {
        var tom = piper.Add("fr_FR-tom-medium", "fr");
        kokoro.Add("ff_siwis", "fr");
        kokoro.ThrowWhileListing = true;
        cloud.Add("nova", null);

        var voices = await selection.ListVoicesAsync();

        Assert.That(voices.Select(voice => voice.Id), Is.EqualTo(new[] { tom.Id, "nova" }));
    }
}
