using System.Threading.Channels;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>
/// Says the answer aloud (US1.9, US1.10, ADR 0006, section 5): the sentences of a turn are synthesized and played
/// in order, the next one being synthesized while the current one plays. Publishes
/// <see cref="FirstSentenceReady"/> when the first sound starts and <see cref="SpeechEnded"/> after the last one.
/// On <see cref="TurnCancelled"/> (US1.12) the sound stops at once and the waiting sentences are dropped; so it does
/// on <see cref="TurnFailed"/>, when the model fails in the middle of an answer (work item 13).
/// </summary>
public sealed class SpeechPlayer :
    IEventHandler<SentenceToSpeak>,
    IEventHandler<AnswerSentencesEnded>,
    IEventHandler<TurnCancelled>,
    IEventHandler<TurnFailed>,
    IDisposable
{
    /// <summary>Sentences synthesized ahead of the one playing: one is enough to hide the synthesis time.</summary>
    private const int ClipsAhead = 1;

    private readonly IVoiceSelection selection;
    private readonly IReadOnlyList<ISpeechEngine> engines;
    private readonly IAudioPlayback playback;
    private readonly ISoundLevelSink levels;
    private readonly IAssistantLanguageReader language;
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly ILogger logger;
    private readonly Lock gate = new();
    private TurnSpeech? current;

    /// <summary>The last turn closed (spoken, cancelled or failed); later messages of it are ignored.</summary>
    private long closedUpTo;

    /// <summary>Creates the player.</summary>
    /// <param name="selection">The voice of each language.</param>
    /// <param name="engines">The engines, to route each voice.</param>
    /// <param name="playback">The host's audio playback, on the output chosen in the audio slice.</param>
    /// <param name="levels">Where the voice level goes, for the orb.</param>
    /// <param name="language">The language of the assistant, which picks the voice.</param>
    /// <param name="bus">The bus, for the messages of the state machine.</param>
    /// <param name="time">Clock of the level steps.</param>
    /// <param name="logger">Log of the failures.</param>
    public SpeechPlayer(
        IVoiceSelection selection,
        IEnumerable<ISpeechEngine> engines,
        IAudioPlayback playback,
        ISoundLevelSink levels,
        IAssistantLanguageReader language,
        IMessageBus bus,
        TimeProvider time,
        ILogger<SpeechPlayer>? logger = null)
    {
        this.selection = selection;
        this.engines = [.. engines];
        this.playback = playback;
        this.levels = levels;
        this.language = language;
        this.bus = bus;
        this.time = time;
        this.logger = logger ?? NullLogger<SpeechPlayer>.Instance;
    }

    /// <summary>The speech of the turn running now, or <see langword="null"/>; for the tests.</summary>
    internal Task? Running
    {
        get
        {
            lock (gate)
            {
                return current?.Work;
            }
        }
    }

    /// <summary>The speech of the last turn started, kept after it ends; for the tests.</summary>
    internal Task LastSpeech { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(SentenceToSpeak message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            QueueFor(message.Turn)?.Sentences.Writer.TryWrite(message.Text.Trim());
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(AnswerSentencesEnded message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        QueueFor(message.Turn)?.Sentences.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnCancelled message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        StopTurn(message.Turn);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnFailed message, CancellationToken cancellationToken)
    {
        // The turn is over: a half-said answer must not go on after the error look. A failure published by this
        // player finds the turn already closed.
        ArgumentNullException.ThrowIfNull(message);
        StopTurn(message.Turn);
        return ValueTask.CompletedTask;
    }

    private void StopTurn(TurnId turn)
    {
        TurnSpeech? stopped = null;
        lock (gate)
        {
            closedUpTo = Math.Max(closedUpTo, turn.Value);
            if (current?.Turn == turn)
            {
                stopped = current;
                current = null;
            }
        }

        if (stopped is not null)
        {
            Stop(stopped);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TurnSpeech? stopped;
        lock (gate)
        {
            stopped = current;
            current = null;
        }

        if (stopped is not null)
        {
            Stop(stopped);
        }
    }

    private void Stop(TurnSpeech speech)
    {
        // Stop net (US1.12): the clip playing is cut by its token and by StopAll, the queue is dropped.
        speech.Sentences.Writer.TryComplete();
        speech.Cancel.Cancel();
        playback.StopAll();
        levels.Report(SoundOrigin.Voice, 0);
    }

    /// <summary>The speech of <paramref name="turn"/>, started on its first message; <see langword="null"/> for a closed turn.</summary>
    private TurnSpeech? QueueFor(TurnId turn)
    {
        TurnSpeech? replaced = null;
        TurnSpeech? speech;
        lock (gate)
        {
            if (turn.Value <= closedUpTo)
            {
                return null;
            }

            if (current?.Turn == turn)
            {
                return current;
            }

            // A newer turn without the cancellation of the old one should not happen (the state machine cancels
            // it first); the old voice must not talk over the new one anyway.
            replaced = current;
            if (replaced is not null)
            {
                closedUpTo = Math.Max(closedUpTo, replaced.Turn.Value);
            }

            speech = new TurnSpeech(turn);
            current = speech;
            speech.Work = Task.Run(() => SpeakAsync(speech));
            LastSpeech = speech.Work;
        }

        if (replaced is not null)
        {
            Stop(replaced);
        }

        return speech;
    }

    private async Task SpeakAsync(TurnSpeech speech)
    {
        var token = speech.Cancel.Token;
        var spoken = false;
        try
        {
            var clips = Channel.CreateBounded<AudioClip>(ClipsAhead);
            var synthesis = SynthesizeAllAsync(speech, clips.Writer, token);
            await foreach (var clip in clips.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                // ReadAllAsync hands out what is already buffered without looking at the token.
                token.ThrowIfCancellationRequested();
                if (!spoken)
                {
                    spoken = true;
                    await bus.PublishAsync(new FirstSentenceReady(speech.Turn), CancellationToken.None).ConfigureAwait(false);
                }

                await PlayAsync(clip, token).ConfigureAwait(false);
            }

            // Lets a synthesis failure through: the reader ended because the writer completed with it.
            await synthesis.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!Close(speech))
            {
                return;
            }

            await bus.PublishAsync<AssistantInput>(
                spoken ? new SpeechEnded(speech.Turn) : new AnswerFinishedWithoutSpeech(speech.Turn),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Interrupted: TurnCancelled already said everything there is to say.
        }
        catch (Exception exception) when (exception is SpeechException or AudioDeviceUnavailableException)
        {
            logger.LogWarning(exception, "The answer of {Turn} could not be spoken.", speech.Turn);
            if (Close(speech))
            {
                await bus.PublishAsync(new TurnFailed(speech.Turn, FaultOf(exception)), CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Speech of {Turn} failed.", speech.Turn);
            if (Close(speech))
            {
                await bus.PublishAsync(
                    new TurnFailed(speech.Turn, new AssistantFault(FaultCause.VoiceMissing, exception.GetType().Name)),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            levels.Report(SoundOrigin.Voice, 0);
        }
    }

    /// <summary>Synthesizes each sentence in order into <paramref name="clips"/>; completes it with the failure if any.</summary>
    private async Task SynthesizeAllAsync(TurnSpeech speech, ChannelWriter<AudioClip> clips, CancellationToken token)
    {
        try
        {
            (SpeechVoice Voice, double Speed)? resolved = null;
            ISpeechEngine? engine = null;
            await foreach (var sentence in speech.Sentences.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (resolved is null)
                {
                    // The voice is fixed for the whole turn, at its first sentence.
                    var spokenLanguage = language.Current;
                    resolved = await selection.ResolveAsync(spokenLanguage, token).ConfigureAwait(false)
                        ?? throw new SpeechException(SpeechFailure.ModelMissing, $"No voice can speak '{spokenLanguage.Code}'.");
                    engine = engines.FirstOrDefault(candidate => candidate.Engine == resolved.Value.Voice.Engine)
                        ?? throw new SpeechException(SpeechFailure.UnknownVoice, $"No engine '{resolved.Value.Voice.Engine}'.");
                }

                var clip = await engine!.SynthesizeAsync(resolved.Value.Voice, sentence, resolved.Value.Speed, token).ConfigureAwait(false);
                await clips.WriteAsync(clip, token).ConfigureAwait(false);
            }

            clips.TryComplete();
        }
        catch (Exception exception)
        {
            clips.TryComplete(exception is OperationCanceledException ? null : exception);
            throw;
        }
    }

    /// <summary>Plays one clip and sends its level to the orb at the pace of the sound.</summary>
    private async Task PlayAsync(AudioClip clip, CancellationToken token)
    {
        var envelope = VoiceEnvelope.Compute(clip);
        var started = time.GetTimestamp();
        var playing = playback.PlayAsync(clip, token).AsTask();
        var step = TimeSpan.FromSeconds(1.0 / VoiceEnvelope.StepsPerSecond);
        // The first level goes out with the sound, even for a clip that ends at once.
        do
        {
            var index = (int)(time.GetElapsedTime(started).TotalSeconds * VoiceEnvelope.StepsPerSecond);
            levels.Report(SoundOrigin.Voice, index < envelope.Length ? envelope[index] : 0);
            if (playing.IsCompleted)
            {
                break;
            }

            await Task.WhenAny(playing, Task.Delay(step, time, token)).ConfigureAwait(false);
        }
        while (!playing.IsCompleted);

        levels.Report(SoundOrigin.Voice, 0);
        await playing.ConfigureAwait(false);
    }

    /// <summary>Ends the turn if it is still the current one; <see langword="false"/> when it was cancelled meanwhile.</summary>
    private bool Close(TurnSpeech speech)
    {
        lock (gate)
        {
            closedUpTo = Math.Max(closedUpTo, speech.Turn.Value);
            if (current != speech)
            {
                return false;
            }

            current = null;
            return true;
        }
    }

    private static AssistantFault FaultOf(Exception exception) => exception switch
    {
        SpeechException { Failure: SpeechFailure.ModelMissing } speech => new AssistantFault(FaultCause.ModelMissing, speech.Message),
        SpeechException speech => new AssistantFault(FaultCause.VoiceMissing, $"{speech.Failure}: {speech.Message}"),
        _ => new AssistantFault(FaultCause.VoiceMissing, exception.Message),
    };

    /// <summary>The speech of one turn: its sentences in order and its cancellation.</summary>
    private sealed class TurnSpeech(TurnId turn)
    {
        public TurnId Turn { get; } = turn;

        public Channel<string> Sentences { get; } = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        public CancellationTokenSource Cancel { get; } = new();

        public Task? Work { get; set; }
    }
}
