using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="ISpeechRecognition"/> (ADR 0003, section 3): no model, no native library. Every
/// utterance becomes the same request, in the language asked for.
/// </summary>
public sealed class SimulatedSpeechRecognition : ISpeechRecognition
{
    /// <summary>What is "heard" in English.</summary>
    public const string EnglishRequest = "What time is it?";

    /// <summary>What is "heard" in French.</summary>
    public const string FrenchRequest = "Quelle heure est-il ?";

    /// <summary>
    /// When set, the Whisper file is "gone": loading fails and the model already loaded fails at the next
    /// transcription, as if the file had been removed (stage1/15-faults).
    /// </summary>
    public bool Missing { get; set; }

    /// <inheritdoc />
    public ValueTask<ISpeechRecognizer> LoadAsync(RecognizerSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return Missing
            ? throw MissingModel(spec)
            : ValueTask.FromResult<ISpeechRecognizer>(new Recognizer(this, spec));
    }

    private static SpeechRecognitionException MissingModel(RecognizerSpec spec) =>
        new(SpeechRecognitionFailure.ModelMissing, "Simulated: the Whisper file is missing.", $"models/whisper/{spec.Model}.bin");

    private sealed class Recognizer(SimulatedSpeechRecognition owner, RecognizerSpec spec) : ISpeechRecognizer
    {
        public RecognizerSpec Spec => spec;

        public ComputeDevice Device => spec.Device;

        public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(ReadOnlyMemory<float> samples, AssistantLanguage language, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(language);
            if (owner.Missing)
            {
                throw MissingModel(spec);
            }

            // About the time of large-v3-turbo on the graphics card (spike E4).
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            return [new TranscriptSegment(" " + (language == AssistantLanguage.French ? FrenchRequest : EnglishRequest), 0.01f)];
        }

        public void Dispose()
        {
        }
    }
}
