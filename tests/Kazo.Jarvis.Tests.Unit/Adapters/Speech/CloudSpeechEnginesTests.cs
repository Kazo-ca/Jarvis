using System.Net;
using Kazo.Jarvis.Adapters.Speech;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Tests.Unit.Adapters.Llm;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Speech;

/// <summary>
/// The cloud voices (US1.10): listed only with a key in the vault, the calls the services expect, the raw sound
/// they return, and the causes of a failure. No real call leaves the test.
/// </summary>
[TestFixture]
public sealed class CloudSpeechEnginesTests
{
    private static readonly byte[] TwoSamples = [0x00, 0x40, 0x00, 0xC0];

    private FakeVault vault = null!;

    [SetUp]
    public void SetUp() => vault = new FakeVault();

    [Test]
    public void Pcm16_becomes_floats()
    {
        Assert.That(CloudSpeech.FromPcm16([0x00, 0x40, 0x00, 0xC0, 0xFF, 0x7F, 0x01]), Is.EqualTo(new[] { 0.5f, -0.5f, 32767f / 32768 }));
    }

    [Test]
    public async Task OpenAi_lists_nothing_without_its_key_then_its_voices_for_every_language()
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("no call expected")));
        var engine = new OpenAiSpeechEngine(http, vault);
        Assert.That(await engine.ListVoicesAsync(), Is.Empty);

        await vault.SetAsync(SpeechKeys.OpenAi, new SecretValue("sk-test"));
        var voices = await engine.ListVoicesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(voices.Select(voice => voice.Id), Does.Contain("nova").And.Contain("onyx"));
            Assert.That(voices, Has.All.Matches<SpeechVoice>(voice => voice.Language is null && voice.LeavesMachine && voice.Engine == "openai"));
        });
    }

    [Test]
    public async Task OpenAi_asks_for_raw_sound_with_the_key_voice_and_speed()
    {
        await vault.SetAsync(SpeechKeys.OpenAi, new SecretValue("sk-test"));
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(TwoSamples) });
        using var http = new HttpClient(handler);
        var engine = new OpenAiSpeechEngine(http, vault);

        var clip = await engine.SynthesizeAsync(new SpeechVoice("openai", "nova", "Nova", null, true), "Bonsoir.", 1.2);

        var (request, body) = handler.Received.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("https://api.openai.com/v1/audio/speech")));
            Assert.That(request.Headers.Authorization?.ToString(), Is.EqualTo("Bearer sk-test"));
            Assert.That(body, Is.EqualTo("{\"model\":\"gpt-4o-mini-tts\",\"input\":\"Bonsoir.\",\"voice\":\"nova\",\"response_format\":\"pcm\",\"speed\":1.2}"));
            Assert.That(clip.SampleRate, Is.EqualTo(24_000));
            Assert.That(clip.Samples.ToArray(), Is.EqualTo(new[] { 0.5f, -0.5f }));
        });
    }

    [TestCase(HttpStatusCode.Unauthorized, SpeechFailure.KeyRefused)]
    [TestCase(HttpStatusCode.NotFound, SpeechFailure.UnknownVoice)]
    [TestCase(HttpStatusCode.InternalServerError, SpeechFailure.Failed)]
    public async Task OpenAi_names_the_cause_of_a_refusal(HttpStatusCode status, SpeechFailure failure)
    {
        await vault.SetAsync(SpeechKeys.OpenAi, new SecretValue("sk-test"));
        using var http = new HttpClient(new StubHandler((_, _) => new HttpResponseMessage(status)));
        var engine = new OpenAiSpeechEngine(http, vault);

        var exception = Assert.ThrowsAsync<SpeechException>(async () =>
            await engine.SynthesizeAsync(new SpeechVoice("openai", "nova", "Nova", null, true), "Bonsoir.", 1));

        Assert.That(exception!.Failure, Is.EqualTo(failure));
    }

    [Test]
    public void A_cloud_voice_without_its_key_says_so_and_an_unreachable_service_too()
    {
        using var http = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("offline")));
        var openAi = new OpenAiSpeechEngine(http, vault);
        var elevenLabs = new ElevenLabsSpeechEngine(http, vault);
        var voice = new SpeechVoice("elevenlabs", "abc", "Abc", null, true);

        var missing = Assert.ThrowsAsync<SpeechException>(async () => await openAi.SynthesizeAsync(voice, "x", 1));
        vault.Secrets[SpeechKeys.ElevenLabs.Value] = "xi-test";
        var unreachable = Assert.ThrowsAsync<SpeechException>(async () => await elevenLabs.SynthesizeAsync(voice, "x", 1));

        Assert.Multiple(() =>
        {
            Assert.That(missing!.Failure, Is.EqualTo(SpeechFailure.KeyMissing));
            Assert.That(unreachable!.Failure, Is.EqualTo(SpeechFailure.Unreachable));
        });
    }

    [Test]
    public async Task ElevenLabs_lists_the_voices_of_the_account_and_reads_them_again_later()
    {
        await vault.SetAsync(SpeechKeys.ElevenLabs, new SecretValue("xi-test"));
        var handler = new StubHandler((_, _) => StubHandler.Json(
            """{"voices":[{"voice_id":"v2","name":"Rachel"},{"voice_id":"v1","name":"Adam"},{"voice_id":"","name":"broken"}]}"""));
        using var http = new HttpClient(handler);
        var time = new FakeTimeProvider();
        var engine = new ElevenLabsSpeechEngine(http, vault, time);

        var voices = await engine.ListVoicesAsync();
        await engine.ListVoicesAsync();
        var callsBefore = handler.Received.Count;
        time.Advance(TimeSpan.FromMinutes(11));
        await engine.ListVoicesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(voices.Select(voice => voice.Id), Is.EqualTo(new[] { "v1", "v2" }));
            Assert.That(voices.Select(voice => voice.Name), Is.EqualTo(new[] { "ElevenLabs Adam", "ElevenLabs Rachel" }));
            Assert.That(voices, Has.All.Matches<SpeechVoice>(voice => voice.Language is null && voice.LeavesMachine));
            Assert.That(handler.Received[0].Request.Headers.GetValues("xi-api-key"), Is.EqualTo(new[] { "xi-test" }));
            Assert.That(callsBefore, Is.EqualTo(1));
            Assert.That(handler.Received, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ElevenLabs_lists_nothing_with_a_refused_key()
    {
        await vault.SetAsync(SpeechKeys.ElevenLabs, new SecretValue("xi-wrong"));
        using var http = new HttpClient(new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        Assert.That(await new ElevenLabsSpeechEngine(http, vault).ListVoicesAsync(), Is.Empty);
    }

    [Test]
    public async Task ElevenLabs_asks_for_raw_sound_with_its_model_and_a_speed_it_accepts()
    {
        await vault.SetAsync(SpeechKeys.ElevenLabs, new SecretValue("xi-test"));
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(TwoSamples) });
        using var http = new HttpClient(handler);

        var clip = await new ElevenLabsSpeechEngine(http, vault).SynthesizeAsync(new SpeechVoice("elevenlabs", "v 1", "Adam", null, true), "Hello.", 2.0);

        var (request, body) = handler.Received.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.elevenlabs.io/v1/text-to-speech/v%201?output_format=pcm_24000"));
            Assert.That(body, Is.EqualTo("{\"text\":\"Hello.\",\"model_id\":\"eleven_flash_v2_5\",\"voice_settings\":{\"speed\":1.2}}"));
            Assert.That(clip.Samples.Length, Is.EqualTo(2));
        });
    }
}
