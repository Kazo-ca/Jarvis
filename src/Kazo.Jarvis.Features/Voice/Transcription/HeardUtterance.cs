namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>The last utterance transcribed, shown on the settings page to check the microphone and the model.</summary>
/// <param name="Text">What Whisper understood; empty when it heard no words.</param>
/// <param name="Speech">Length of the audio transcribed.</param>
/// <param name="Elapsed">Time spent transcribing.</param>
/// <param name="Detector">The detector that found the end of the utterance.</param>
public sealed record HeardUtterance(string Text, TimeSpan Speech, TimeSpan Elapsed, EndOfSpeechDetector Detector);
