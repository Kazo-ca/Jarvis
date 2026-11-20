using Kazo.Jarvis.Features.Voice.Transcription;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>The end of the utterance (US1.7, ADR 0006, section 3), counted in samples.</summary>
[TestFixture]
public sealed class UtteranceEndpointerTests
{
    // 32 ms frames, as Silero.
    private const int Frame = 512;

    private static readonly EndpointerTiming Timing = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(25));

    [Test]
    public void Half_a_second_of_silence_after_speech_ends_the_utterance()
    {
        var endpointer = new UtteranceEndpointer(Timing);

        Assert.Multiple(() =>
        {
            Assert.That(Push(endpointer, false, 10), Is.EqualTo(EndpointDecision.Continue));
            Assert.That(Push(endpointer, true, 30), Is.EqualTo(EndpointDecision.Continue));
            Assert.That(Push(endpointer, false, 15), Is.EqualTo(EndpointDecision.Continue), "480 ms of silence");
            Assert.That(Push(endpointer, false, 1), Is.EqualTo(EndpointDecision.Ended), "512 ms of silence");
            Assert.That(endpointer.SpeechStart, Is.EqualTo(10 * Frame));
            Assert.That(endpointer.End, Is.EqualTo(56 * Frame));
        });
    }

    [Test]
    public void A_pause_shorter_than_the_silence_does_not_end_the_utterance()
    {
        var endpointer = new UtteranceEndpointer(Timing);
        Push(endpointer, true, 20);
        Push(endpointer, false, 12);

        Assert.That(Push(endpointer, true, 20), Is.EqualTo(EndpointDecision.Continue));
        Assert.That(Push(endpointer, false, 15), Is.EqualTo(EndpointDecision.Continue), "the silence counts again from zero");
    }

    [Test]
    public void A_click_shorter_than_the_minimum_speech_is_not_an_utterance()
    {
        var endpointer = new UtteranceEndpointer(Timing);

        // 4 frames = 128 ms, below 150 ms.
        Push(endpointer, true, 4);
        Push(endpointer, false, 30);

        Assert.That(endpointer.Started, Is.False);
    }

    [Test]
    public void Without_speech_the_timeout_says_nothing_was_heard()
    {
        var endpointer = new UtteranceEndpointer(Timing);

        // 8 s = 250 frames.
        Assert.That(Push(endpointer, false, 249), Is.EqualTo(EndpointDecision.Continue));
        Assert.That(Push(endpointer, false, 1), Is.EqualTo(EndpointDecision.NothingHeard));
    }

    [Test]
    public void Speech_started_before_the_timeout_keeps_listening_after_it()
    {
        var endpointer = new UtteranceEndpointer(Timing);
        Push(endpointer, false, 240);

        Assert.That(Push(endpointer, true, 30), Is.EqualTo(EndpointDecision.Continue));
    }

    [Test]
    public void The_longest_utterance_is_cut()
    {
        var endpointer = new UtteranceEndpointer(Timing with { MaxUtterance = TimeSpan.FromSeconds(2) });

        // 2 s = 62.5 frames: the 63rd ends it.
        Assert.That(Push(endpointer, true, 62), Is.EqualTo(EndpointDecision.Continue));
        Assert.That(Push(endpointer, true, 1), Is.EqualTo(EndpointDecision.Ended));
    }

    private static EndpointDecision Push(UtteranceEndpointer endpointer, bool speech, int frames)
    {
        var decision = EndpointDecision.Continue;
        for (var i = 0; i < frames; i++)
        {
            decision = endpointer.Push(speech, Frame);
        }

        return decision;
    }
}
