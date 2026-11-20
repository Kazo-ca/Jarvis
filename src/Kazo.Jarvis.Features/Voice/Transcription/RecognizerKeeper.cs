using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// Keeps the Whisper model in memory (ADR 0006, section 4): loaded once at start, again when the model or the
/// device changes, and tried again before a transcription when it was missing (the file may have been put in
/// place since). One transcription at a time; a reload waits for the current one.
/// </summary>
public sealed partial class RecognizerKeeper : IHostedService, IDisposable
{
    private readonly ISpeechRecognition recognition;
    private readonly ISettingsStore settings;
    private readonly ILogger<RecognizerKeeper> logger;
    private readonly SemaphoreSlim use = new(1, 1);
    private readonly Lock gate = new();
    private IDisposable? watch;
    private ISpeechRecognizer? recognizer;
    private Task loading = Task.CompletedTask;
    private RecognizerStatus status;

    /// <summary>Creates the keeper.</summary>
    /// <param name="recognition">Loads the models.</param>
    /// <param name="settings">The settings, for <see cref="TranscriptionOptions.Section"/>.</param>
    /// <param name="logger">Logs the loads and their failures.</param>
    public RecognizerKeeper(ISpeechRecognition recognition, ISettingsStore settings, ILogger<RecognizerKeeper> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.recognition = recognition;
        this.settings = settings;
        this.logger = logger;
        status = new RecognizerStatus(RecognizerState.NotLoaded, SpecOf(settings.Read(TranscriptionOptions.Section)));
    }

    /// <summary>Raised after each change of <see cref="Status"/>, on any thread.</summary>
    public event Action? StatusChanged;

    /// <summary>Where the loading of the model stands.</summary>
    public RecognizerStatus Status
    {
        get
        {
            lock (gate)
            {
                return status;
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Watch before the first load: a change between the two is not missed.
        watch = settings.Watch(TranscriptionOptions.Section, options => Reload(SpecOf(options), onlyIfChanged: true));
        Reload(SpecOf(settings.Read(TranscriptionOptions.Section)), onlyIfChanged: false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        watch?.Dispose();
        watch = null;
        return Task.CompletedTask;
    }

    /// <summary>The load in progress, if any: completes when the model is ready or has failed.</summary>
    /// <returns>A task that never faults.</returns>
    public Task WhenLoadedAsync()
    {
        lock (gate)
        {
            return loading;
        }
    }

    /// <summary>
    /// Transcribes an utterance with the current model, after waiting for a load in progress. A model missing
    /// at start is looked for again first.
    /// </summary>
    /// <param name="samples">The utterance, 16 kHz mono.</param>
    /// <param name="language">The language of the assistant.</param>
    /// <param name="cancellationToken">Stops the wait and the transcription.</param>
    /// <returns>The segments Whisper found.</returns>
    /// <exception cref="SpeechRecognitionException">No model could be loaded, or the transcription failed.</exception>
    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(ReadOnlyMemory<float> samples, AssistantLanguage language, CancellationToken cancellationToken)
    {
        await WhenLoadedAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (Status.State is RecognizerState.Missing or RecognizerState.Failed or RecognizerState.NotLoaded)
        {
            Reload(Status.Spec, onlyIfChanged: false);
            await WhenLoadedAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await use.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = recognizer;
            if (current is null)
            {
                var failed = Status;
                throw new SpeechRecognitionException(
                    failed.State == RecognizerState.Missing ? SpeechRecognitionFailure.ModelMissing : SpeechRecognitionFailure.LoadFailed,
                    failed.Detail ?? "The Whisper model is not loaded.",
                    failed.ModelPath);
            }

            return await current.TranscribeAsync(samples, language, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            use.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        watch?.Dispose();
        recognizer?.Dispose();
        use.Dispose();
    }

    private static RecognizerSpec SpecOf(TranscriptionOptions options) => new(options.Model, options.Device);

    private void Reload(RecognizerSpec spec, bool onlyIfChanged)
    {
        lock (gate)
        {
            if (onlyIfChanged && spec == status.Spec)
            {
                return;
            }

            // Loads follow each other: the last one asked for wins.
            var previous = loading;
            status = new RecognizerStatus(RecognizerState.Loading, spec);
            loading = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                await LoadAsync(spec).ConfigureAwait(false);
            });
        }

        StatusChanged?.Invoke();
    }

    private async Task LoadAsync(RecognizerSpec spec)
    {
        // A newer request replaced this one while it waited.
        if (Status.Spec != spec)
        {
            return;
        }

        await use.WaitAsync().ConfigureAwait(false);
        RecognizerStatus next;
        try
        {
            // Free the old model first: the graphics card has about 600 MiB to spare (ADR 0006, section 6).
            recognizer?.Dispose();
            recognizer = null;
            try
            {
                recognizer = await recognition.LoadAsync(spec).ConfigureAwait(false);
                next = new RecognizerStatus(RecognizerState.Ready, spec, recognizer.Device);
                LogLoaded(spec.Model, spec.Device, recognizer.Device);
            }
            catch (SpeechRecognitionException exception)
            {
                var state = exception.Failure == SpeechRecognitionFailure.ModelMissing ? RecognizerState.Missing : RecognizerState.Failed;
                next = new RecognizerStatus(state, spec, null, exception.ModelPath, exception.Message);
                LogLoadFailed(spec.Model, exception.Failure, exception.Message);
            }
        }
        finally
        {
            use.Release();
        }

        lock (gate)
        {
            if (status.Spec == spec)
            {
                status = next;
            }
        }

        StatusChanged?.Invoke();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper model {Model} loaded for {Wanted}, running on {Device}")]
    private partial void LogLoaded(WhisperModel model, ComputeDevice wanted, ComputeDevice device);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Whisper model {Model} could not be loaded: {Failure}, {Detail}")]
    private partial void LogLoadFailed(WhisperModel model, SpeechRecognitionFailure failure, string detail);
}
