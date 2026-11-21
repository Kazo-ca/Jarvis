using System.Buffers;
using System.Threading.Channels;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// Listens to a spoken turn (US1.7): from <see cref="TurnStarted"/>, reads the microphone through a voice
/// activity detector, publishes <see cref="UtteranceEnded"/> after the silence that ends the utterance (or
/// <see cref="NothingHeard"/> after the timeout), then transcribes it with Whisper and publishes
/// <see cref="UtteranceTranscribed"/>. A <see cref="TurnCancelled"/> stops it at any point.
/// <para>
/// It also opens the single listening that follows the question of a confirmation request (ADR 0023, section 3):
/// same end of speech, same Whisper, but what is heard only goes out as <see cref="ConfirmationAnswerHeard"/>. It
/// publishes none of the messages of a turn, so the phase does not change and no model receives the text.
/// </para>
/// </summary>
/// <remarks>
/// The capture thread only copies the samples into a channel; the detector and Whisper run on a pool thread.
/// </remarks>
public sealed partial class UtteranceListener :
    IEventHandler<TurnStarted>,
    IEventHandler<TurnCancelled>,
    IEventHandler<ConfirmationQuestionSaid>,
    IEventHandler<ConfirmationClosed>,
    IDisposable
{
    /// <summary>
    /// Audio kept before the start of speech, so the first syllable is not cut: detectors answer a little late.
    /// Not set by an ADR: 300 ms (provisional choice of work item stage1/10-transcription).
    /// </summary>
    public static readonly TimeSpan PreRoll = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Without any sample for this long (microphone unplugged, driver stalled), the listening stops as if the
    /// silence had come: what was said is transcribed, otherwise nothing was heard.
    /// </summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(3);

    private readonly IMicrophoneSource microphone;
    private readonly IMessageBus bus;
    private readonly ISettingsStore settings;
    private readonly RecognizerKeeper recognizer;
    private readonly IAssistantLanguageReader language;
    private readonly IVoiceActivityDetection? silero;
    private readonly TimeProvider time;
    private readonly ILogger<UtteranceListener> logger;
    private readonly Lock gate = new();
    private Session? current;

    /// <summary>The listening of a confirmation answer, its request, and the last request closed.</summary>
    private Session? answer;
    private Guid answerOf;
    private Guid closedRequest;
    private HeardUtterance? lastHeard;
    private int sileroWarned;

    /// <summary>Creates the listener.</summary>
    /// <param name="microphone">What the microphone captures.</param>
    /// <param name="bus">Where the end of the utterance and the transcription go.</param>
    /// <param name="settings">The settings, for <see cref="TranscriptionOptions.Section"/>.</param>
    /// <param name="recognizer">The Whisper model.</param>
    /// <param name="language">The language of the transcription (ADR 0008).</param>
    /// <param name="time">The clock of the stall guard and of the measures.</param>
    /// <param name="logger">Logs each utterance and the failures.</param>
    /// <param name="silero">Silero, when the host provides it; otherwise the energy threshold is used.</param>
    public UtteranceListener(
        IMicrophoneSource microphone,
        IMessageBus bus,
        ISettingsStore settings,
        RecognizerKeeper recognizer,
        IAssistantLanguageReader language,
        TimeProvider time,
        ILogger<UtteranceListener> logger,
        IVoiceActivityDetection? silero = null)
    {
        this.microphone = microphone;
        this.bus = bus;
        this.settings = settings;
        this.recognizer = recognizer;
        this.language = language;
        this.time = time;
        this.logger = logger;
        this.silero = silero;
    }

    /// <summary>Raised after an utterance is transcribed, on any thread.</summary>
    public event Action? Heard;

    /// <summary>The last utterance transcribed since start, or <see langword="null"/>.</summary>
    public HeardUtterance? LastHeard => Volatile.Read(ref lastHeard);

    /// <summary>Whether a spoken turn is being listened to or transcribed.</summary>
    public bool IsBusy
    {
        get
        {
            lock (gate)
            {
                return current is not null;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnStarted message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Input != TurnInput.Voice)
        {
            return ValueTask.CompletedTask;
        }

        var options = settings.Read(TranscriptionOptions.Section);
        var session = new Session(message.Turn);
        Session? previous;
        Session? previousAnswer;
        lock (gate)
        {
            previous = current;
            current = session;
            previousAnswer = answer;
            answer = null;
        }

        previous?.Cancel();
        previousAnswer?.Cancel();

        // Subscribe now, on the bus thread, so no sample after the activation is missed.
        session.Subscription = microphone.Subscribe(session.Accept);
        _ = Task.Run(() => RunAsync(session, options));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnCancelled message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Session? cancelled = null;
        Session? cancelledAnswer = null;
        lock (gate)
        {
            if (current?.Turn == message.Turn)
            {
                cancelled = current;
                current = null;
            }

            if (answer?.Turn == message.Turn)
            {
                cancelledAnswer = answer;
                answer = null;
            }
        }

        cancelled?.Cancel();
        cancelledAnswer?.Cancel();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ConfirmationQuestionSaid message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.Listen)
        {
            return ValueTask.CompletedTask;
        }

        var options = settings.Read(TranscriptionOptions.Section);
        var session = new Session(message.Turn);
        Session? previous;
        lock (gate)
        {
            // Closed while the question ended, or a turn that is listening: no answer to hear.
            if (closedRequest == message.Id || current is not null)
            {
                return ValueTask.CompletedTask;
            }

            previous = answer;
            answer = session;
            answerOf = message.Id;
        }

        previous?.Cancel();
        session.Subscription = microphone.Subscribe(session.Accept);
        _ = Task.Run(() => RunAnswerAsync(session, message.Id, options));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ConfirmationClosed message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Session? cancelled = null;
        lock (gate)
        {
            closedRequest = message.Id;
            if (answer is not null && answerOf == message.Id)
            {
                cancelled = answer;
                answer = null;
            }
        }

        cancelled?.Cancel();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Session? session;
        Session? listening;
        lock (gate)
        {
            session = current;
            current = null;
            listening = answer;
            answer = null;
        }

        session?.Cancel();
        listening?.Cancel();
    }

    /// <summary>
    /// The single listening of a confirmation answer. Nothing heard, or a failure, is "no answer": the request stays
    /// open and the click remains possible.
    /// </summary>
    private async Task RunAnswerAsync(Session session, Guid request, TranscriptionOptions options)
    {
        var token = session.Token;
        try
        {
            string? text = null;
            var (detector, _) = CreateDetector(options.Detector);
            Utterance? utterance;
            using (detector)
            {
                utterance = await ListenAsync(session, detector, options.ToTiming(), token).ConfigureAwait(false);
            }

            session.StopListening();
            if (utterance is not null)
            {
                var segments = await recognizer.TranscribeAsync(utterance.Samples, language.Current, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                text = TranscriptText.From(segments);
            }

            // The length only: the words of the answer are not logged.
            LogAnswerHeard(session.Turn, text?.Length ?? 0);
            await bus.PublishAsync(new ConfirmationAnswerHeard(request, text), CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The request closed or the turn was cancelled: nothing to publish.
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            LogAnswerFailed(exception, session.Turn);
            await bus.PublishAsync(new ConfirmationAnswerHeard(request, null), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            session.StopListening();
            lock (gate)
            {
                if (answer == session)
                {
                    answer = null;
                }
            }
        }
    }

    private async Task RunAsync(Session session, TranscriptionOptions options)
    {
        var token = session.Token;
        try
        {
            var (detector, kind) = CreateDetector(options.Detector);
            Utterance? utterance;
            using (detector)
            {
                utterance = await ListenAsync(session, detector, options.ToTiming(), token).ConfigureAwait(false);
            }

            session.StopListening();
            if (utterance is null)
            {
                LogNothingHeard(session.Turn, kind);
                await bus.PublishAsync(new NothingHeard(session.Turn), CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await bus.PublishAsync(new UtteranceEnded(session.Turn), CancellationToken.None).ConfigureAwait(false);
            await TranscribeAsync(session, utterance, kind, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The turn was cancelled: nothing to publish.
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            LogFailed(exception, session.Turn);
            await bus.PublishAsync(
                new TurnFailed(session.Turn, new AssistantFault(FaultCause.ModelMissing, exception.Message, FaultOrigin.Transcription)),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            session.StopListening();
            lock (gate)
            {
                if (current == session)
                {
                    current = null;
                }
            }
        }
    }

    private async Task<Utterance?> ListenAsync(Session session, IVoiceActivityDetector detector, EndpointerTiming timing, CancellationToken token)
    {
        var endpointer = new UtteranceEndpointer(timing);
        var frameLength = detector.FrameLength;
        var audio = session.Audio;
        var processed = 0;
        while (true)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(token);
            stall.CancelAfter(StallTimeout);
            float[] block;
            try
            {
                block = await session.Samples.Reader.ReadAsync(stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // No sample for a while: end with what was heard.
                LogStalled(session.Turn);
                return endpointer.Started ? Cut(endpointer.SpeechStart, audio.WrittenCount) : null;
            }

            audio.Write(block);
            while (audio.WrittenCount - processed >= frameLength)
            {
                var decision = endpointer.Push(detector.IsSpeech(audio.WrittenSpan.Slice(processed, frameLength)), frameLength);
                processed += frameLength;
                switch (decision)
                {
                    case EndpointDecision.Ended:
                        return Cut(endpointer.SpeechStart, (int)endpointer.End);
                    case EndpointDecision.NothingHeard:
                        return null;
                }
            }
        }

        Utterance Cut(long speechStart, int end)
        {
            var start = (int)Math.Max(0, speechStart - MicrophoneFormat.SamplesFor(PreRoll));
            return new Utterance(audio.WrittenMemory[start..end].ToArray(), time.GetTimestamp());
        }
    }

    private async Task TranscribeAsync(Session session, Utterance utterance, EndOfSpeechDetector kind, CancellationToken token)
    {
        var spoken = language.Current;
        IReadOnlyList<TranscriptSegment> segments;
        try
        {
            segments = await recognizer.TranscribeAsync(utterance.Samples, spoken, token).ConfigureAwait(false);
        }
        catch (SpeechRecognitionException exception)
        {
            LogRecognitionFailed(session.Turn, exception.Failure, exception.Message);
            var detail = exception.ModelPath is { } path ? $"{exception.Message} ({path})" : exception.Message;
            await bus.PublishAsync(
                new TurnFailed(session.Turn, new AssistantFault(FaultCause.ModelMissing, detail, FaultOrigin.Transcription)),
                CancellationToken.None).ConfigureAwait(false);
            return;
        }

        token.ThrowIfCancellationRequested();
        var text = TranscriptText.From(segments);
        var speech = TimeSpan.FromSeconds((double)utterance.Samples.Length / MicrophoneFormat.SampleRate);
        var elapsed = time.GetElapsedTime(utterance.EndedAt);
        LogTranscribed(session.Turn, (long)speech.TotalMilliseconds, (long)elapsed.TotalMilliseconds, kind, text.Length);
        Volatile.Write(ref lastHeard, new HeardUtterance(text, speech, elapsed, kind));
        Heard?.Invoke();
        await bus.PublishAsync(new UtteranceTranscribed(session.Turn, text, spoken, speech, elapsed), CancellationToken.None).ConfigureAwait(false);
    }

    private (IVoiceActivityDetector Detector, EndOfSpeechDetector Kind) CreateDetector(EndOfSpeechDetector wanted)
    {
        if (wanted == EndOfSpeechDetector.Silero && silero is not null)
        {
            try
            {
                return (silero.Create(), EndOfSpeechDetector.Silero);
            }
            catch (SpeechRecognitionException exception)
            {
                // ADR 0006, section 3: the energy threshold is the fallback. Said once in the log, not at every turn.
                if (Interlocked.Exchange(ref sileroWarned, 1) == 0)
                {
                    LogSileroUnavailable(exception.Failure, exception.Message);
                }
            }
        }

        return (new EnergyVoiceActivityDetector(), EndOfSpeechDetector.Energy);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Utterance of {Turn}: {SpeechMs} ms of audio transcribed in {ElapsedMs} ms (end found by {Detector}), {Characters} characters")]
    private partial void LogTranscribed(TurnId turn, long speechMs, long elapsedMs, EndOfSpeechDetector detector, int characters);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nothing heard in {Turn} (detector {Detector})")]
    private partial void LogNothingHeard(TurnId turn, EndOfSpeechDetector detector);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No microphone sample for a while during {Turn}: listening stops")]
    private partial void LogStalled(TurnId turn);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Silero cannot be used ({Failure}: {Detail}); the end of speech uses the energy threshold")]
    private partial void LogSileroUnavailable(SpeechRecognitionFailure failure, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transcription of {Turn} failed: {Failure}, {Detail}")]
    private partial void LogRecognitionFailed(TurnId turn, SpeechRecognitionFailure failure, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Listening of {Turn} failed")]
    private partial void LogFailed(Exception exception, TurnId turn);

    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation answer of {Turn} heard: {Characters} characters")]
    private partial void LogAnswerHeard(TurnId turn, int characters);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Listening to the confirmation answer of {Turn} failed; the request stays open")]
    private partial void LogAnswerFailed(Exception exception, TurnId turn);

    private sealed record Utterance(float[] Samples, long EndedAt);

    /// <summary>One listening: its samples, its audio so far, its cancellation.</summary>
    private sealed class Session(TurnId turn)
    {
        private readonly CancellationTokenSource cancellation = new();

        public TurnId Turn { get; } = turn;

        public Channel<float[]> Samples { get; } = Channel.CreateUnbounded<float[]>(new UnboundedChannelOptions { SingleReader = true });

        public ArrayBufferWriter<float> Audio { get; } = new(MicrophoneFormat.SamplesFor(TimeSpan.FromSeconds(10)));

        public CancellationToken Token => cancellation.Token;

        public IDisposable? Subscription
        {
            set => Volatile.Write(ref subscriptionField, value);
        }

        public void Accept(ReadOnlySpan<float> samples) => Samples.Writer.TryWrite(samples.ToArray());

        public void StopListening()
        {
            Interlocked.Exchange(ref subscriptionField, null)?.Dispose();
            Samples.Writer.TryComplete();
        }

        public void Cancel()
        {
            // Cancel first: the reader then sees a cancellation, not only a closed channel.
            cancellation.Cancel();
            StopListening();
        }

        private IDisposable? subscriptionField;
    }
}
