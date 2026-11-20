using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>How the end of an utterance is detected (ADR 0006, section 3).</summary>
public enum EndOfSpeechDetector
{
    /// <summary>Silero by sherpa-onnx: the default.</summary>
    Silero,

    /// <summary>A simple energy threshold: the fallback named by ADR 0006, used too when Silero cannot load.</summary>
    Energy,
}

/// <summary>Settings of the transcription (US1.7), section <c>Voice.Transcription</c> of <c>config.json</c>.</summary>
public sealed record TranscriptionOptions
{
    /// <summary>Shortest silence that ends an utterance, in milliseconds.</summary>
    public const int MinimumSilence = 200;

    /// <summary>Longest silence that ends an utterance, in milliseconds.</summary>
    public const int MaximumSilence = 3000;

    /// <summary>Shortest wait for speech after an activation, in seconds.</summary>
    public const int MinimumTimeout = 2;

    /// <summary>Longest wait for speech after an activation, in seconds.</summary>
    public const int MaximumTimeout = 30;

    /// <summary>
    /// Longest utterance, in seconds. The listening buffer of the kernel keeps 30 s (work item
    /// stage1/8-audio-devices); the slice keeps its own copy, but stays below that length.
    /// </summary>
    public const int MaximumUtterance = 28;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<TranscriptionOptions> Section { get; } = new(
        "Voice.Transcription",
        () => new TranscriptionOptions(),
        Validate);

    /// <summary>The Whisper model: <c>large-v3-turbo</c> by default (ADR 0006, section 4).</summary>
    public WhisperModel Model { get; init; } = WhisperModel.LargeV3Turbo;

    /// <summary>Where Whisper runs: the graphics card by default.</summary>
    public ComputeDevice Device { get; init; } = ComputeDevice.Gpu;

    /// <summary>How the end of the utterance is detected: Silero by default.</summary>
    public EndOfSpeechDetector Detector { get; init; } = EndOfSpeechDetector.Silero;

    /// <summary>Silence that ends the utterance, in milliseconds: 500 by default (ADR 0006, section 3).</summary>
    public int SilenceMilliseconds { get; init; } = 500;

    /// <summary>
    /// Wait for speech after an activation before going back to idle, in seconds. Not set by an ADR: 8 s
    /// (provisional choice of work item stage1/10-transcription).
    /// </summary>
    public int NoSpeechTimeoutSeconds { get; init; } = 8;

    /// <summary>
    /// Longest utterance before it is cut and transcribed anyway, in seconds. Not set by an ADR: 25 s
    /// (provisional choice of work item stage1/10-transcription).
    /// </summary>
    public int MaxUtteranceSeconds { get; init; } = 25;

    /// <summary>The timing used by <see cref="UtteranceEndpointer"/>.</summary>
    /// <returns>The timing.</returns>
    public EndpointerTiming ToTiming() => new(
        TimeSpan.FromMilliseconds(SilenceMilliseconds),
        TimeSpan.FromSeconds(NoSpeechTimeoutSeconds),
        TimeSpan.FromSeconds(MaxUtteranceSeconds));

    private static IReadOnlyList<string> Validate(TranscriptionOptions value)
    {
        var problems = new List<string>();
        if (!Enum.IsDefined(value.Model))
        {
            problems.Add($"'{value.Model}' is not a Whisper model: {string.Join(", ", Enum.GetNames<WhisperModel>())}.");
        }

        if (!Enum.IsDefined(value.Device))
        {
            problems.Add($"'{value.Device}' is not a device: {string.Join(", ", Enum.GetNames<ComputeDevice>())}.");
        }

        if (!Enum.IsDefined(value.Detector))
        {
            problems.Add($"'{value.Detector}' is not a detector: {string.Join(", ", Enum.GetNames<EndOfSpeechDetector>())}.");
        }

        if (value.SilenceMilliseconds is < MinimumSilence or > MaximumSilence)
        {
            problems.Add($"The silence must be from {MinimumSilence} to {MaximumSilence} ms, not {value.SilenceMilliseconds}.");
        }

        if (value.NoSpeechTimeoutSeconds is < MinimumTimeout or > MaximumTimeout)
        {
            problems.Add($"The wait for speech must be from {MinimumTimeout} to {MaximumTimeout} s, not {value.NoSpeechTimeoutSeconds}.");
        }

        if (value.MaxUtteranceSeconds is < 2 or > MaximumUtterance)
        {
            problems.Add($"The longest utterance must be from 2 to {MaximumUtterance} s, not {value.MaxUtteranceSeconds}.");
        }

        return problems;
    }
}
