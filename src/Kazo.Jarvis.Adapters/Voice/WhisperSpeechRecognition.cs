using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Kazo.Jarvis.Adapters.Voice;

/// <summary>
/// <see cref="ISpeechRecognition"/> on Whisper.net (ADR 0006, section 4), as proven by spike E4. The native
/// library is chosen once per process: CUDA when the host ships it and the card answers, the processor
/// otherwise.
/// </summary>
public sealed partial class WhisperSpeechRecognition : ISpeechRecognition
{
    private static readonly Lock RuntimeGate = new();
    private static bool runtimeChosen;

    private readonly JarvisPaths paths;
    private readonly ILogger logger;

    /// <summary>Creates the recognition.</summary>
    /// <param name="paths">Where the models folder is.</param>
    /// <param name="logger">Logs the native library loaded.</param>
    public WhisperSpeechRecognition(JarvisPaths paths, ILogger<WhisperSpeechRecognition>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        this.paths = paths;
        this.logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<ISpeechRecognizer> LoadAsync(RecognizerSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var path = VoiceModelFiles.WhisperPath(paths, spec.Model);
        if (!File.Exists(path))
        {
            throw new SpeechRecognitionException(SpeechRecognitionFailure.ModelMissing, $"The Whisper model {Path.GetFileName(path)} is not in the models folder.", path);
        }

        ChooseRuntime();

        // Loading a model takes seconds of native work: keep it off the caller's thread.
        return await Task.Run(
            () =>
            {
                try
                {
                    var factory = WhisperFactory.FromPath(path, new WhisperFactoryOptions { UseGpu = spec.Device == ComputeDevice.Gpu });
                    var loaded = RuntimeOptions.LoadedLibrary;
                    var device = spec.Device == ComputeDevice.Gpu && loaded == RuntimeLibrary.Cuda ? ComputeDevice.Gpu : ComputeDevice.Cpu;
                    LogRuntime(loaded?.ToString() ?? "none", spec.Model, device);
                    return (ISpeechRecognizer)new Recognizer(factory, spec, device, path);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new SpeechRecognitionException(SpeechRecognitionFailure.LoadFailed, $"Whisper could not load {Path.GetFileName(path)}: {exception.Message}", path, exception);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CUDA first, then the processor (spike E4). Only possible before the first model is loaded.</summary>
    private static void ChooseRuntime()
    {
        lock (RuntimeGate)
        {
            if (!runtimeChosen)
            {
                RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu];
                runtimeChosen = true;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper native library {Library}: model {Model} runs on {Device}")]
    private partial void LogRuntime(string library, WhisperModel model, ComputeDevice device);

    private sealed class Recognizer(WhisperFactory factory, RecognizerSpec spec, ComputeDevice device, string path) : ISpeechRecognizer
    {
        // Whisper.net fixes the language when the processor is built: one processor, rebuilt when it changes.
        private WhisperProcessor? processor;
        private AssistantLanguage? processorLanguage;

        public RecognizerSpec Spec => spec;

        public ComputeDevice Device => device;

        public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(ReadOnlyMemory<float> samples, AssistantLanguage language, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(language);
            try
            {
                var current = ProcessorFor(language);
                var segments = new List<TranscriptSegment>();
                await foreach (var segment in current.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
                {
                    segments.Add(new TranscriptSegment(segment.Text, segment.NoSpeechProbability));
                }

                return segments;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new SpeechRecognitionException(SpeechRecognitionFailure.Failed, $"Whisper failed: {exception.Message}", path, exception);
            }
        }

        public void Dispose()
        {
            processor?.Dispose();
            factory.Dispose();
        }

        private WhisperProcessor ProcessorFor(AssistantLanguage language)
        {
            if (processor is null || processorLanguage != language)
            {
                processor?.Dispose();
                processor = factory.CreateBuilder()
                    .WithLanguage(language.Code)
                    .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 8))
                    .Build();
                processorLanguage = language;
            }

            return processor;
        }
    }
}
