using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>The timing of the end of an utterance (ADR 0006, section 3).</summary>
/// <param name="Silence">Silence after speech that ends the utterance.</param>
/// <param name="NoSpeechTimeout">Wait for speech after the activation; then nothing was heard.</param>
/// <param name="MaxUtterance">Longest utterance, counted from the start of speech; then it ends anyway.</param>
public sealed record EndpointerTiming(TimeSpan Silence, TimeSpan NoSpeechTimeout, TimeSpan MaxUtterance)
{
    /// <summary>
    /// Speech needed, without a pause, before the utterance counts as started: a click or a cough is not a
    /// request. Not set by an ADR: 150 ms (provisional choice of work item stage1/10-transcription).
    /// </summary>
    public TimeSpan MinimumSpeech { get; init; } = TimeSpan.FromMilliseconds(150);
}

/// <summary>What <see cref="UtteranceEndpointer.Push"/> found.</summary>
public enum EndpointDecision
{
    /// <summary>Keep listening.</summary>
    Continue,

    /// <summary>The utterance is over (silence or longest utterance reached).</summary>
    Ended,

    /// <summary>No speech before the timeout.</summary>
    NothingHeard,
}

/// <summary>
/// Finds the end of an utterance from the frame-by-frame answer of a voice activity detector (US1.7). Counts
/// in samples, not in clock time: the same audio always gives the same decision, and the tests need no clock.
/// </summary>
public sealed class UtteranceEndpointer
{
    private readonly long silence;
    private readonly long timeout;
    private readonly long maxUtterance;
    private readonly long minimumSpeech;
    private long speechRun;
    private long silenceRun;

    /// <summary>Creates the endpointer for one listening.</summary>
    /// <param name="timing">The timing.</param>
    public UtteranceEndpointer(EndpointerTiming timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        silence = MicrophoneFormat.SamplesFor(timing.Silence);
        timeout = MicrophoneFormat.SamplesFor(timing.NoSpeechTimeout);
        maxUtterance = MicrophoneFormat.SamplesFor(timing.MaxUtterance);
        minimumSpeech = MicrophoneFormat.SamplesFor(timing.MinimumSpeech);
    }

    /// <summary>Samples read since the start of the listening.</summary>
    public long Position { get; private set; }

    /// <summary>Whether speech was heard; the utterance has started.</summary>
    public bool Started { get; private set; }

    /// <summary>Where the speech started, in samples from the start of the listening; meaningful once <see cref="Started"/>.</summary>
    public long SpeechStart { get; private set; }

    /// <summary>Where the decision <see cref="EndpointDecision.Ended"/> was taken, in samples; the end of the audio to transcribe.</summary>
    public long End { get; private set; }

    /// <summary>Reads the answer of the detector for the next frame.</summary>
    /// <param name="isSpeech">Whether the frame holds speech.</param>
    /// <param name="frameLength">Samples in the frame.</param>
    /// <returns>The decision after this frame. After <see cref="EndpointDecision.Ended"/> or <see cref="EndpointDecision.NothingHeard"/>, stop pushing.</returns>
    public EndpointDecision Push(bool isSpeech, int frameLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameLength);
        Position += frameLength;
        if (isSpeech)
        {
            speechRun += frameLength;
            silenceRun = 0;
            if (!Started && speechRun >= minimumSpeech)
            {
                Started = true;
                SpeechStart = Position - speechRun;
            }
        }
        else
        {
            speechRun = 0;
            if (Started)
            {
                silenceRun += frameLength;
            }
        }

        if (Started && (silenceRun >= silence || Position - SpeechStart >= maxUtterance))
        {
            End = Position;
            return EndpointDecision.Ended;
        }

        return !Started && Position >= timeout ? EndpointDecision.NothingHeard : EndpointDecision.Continue;
    }
}
