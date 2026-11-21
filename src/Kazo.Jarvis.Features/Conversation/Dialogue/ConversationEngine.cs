using System.Text;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Conversation;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The full turn (US1.9), by messages only: a typed request (<see cref="TurnStarted"/> with
/// <see cref="TurnInput.Text"/>) or what the user said (<see cref="UtteranceTranscribed"/>) goes to the default model
/// with the personality and the recent history; the answer arrives in a stream and, for a spoken request, each
/// complete sentence goes to the voice at once (<see cref="SentenceToSpeak"/>). A typed request gets a written answer
/// only: Jarvis speaks only when spoken to (stage 1 plan, answer 3).
/// <para>
/// On <see cref="TurnCancelled"/> (an interruption by the wake word, the hotkey or a click, US1.12) the generation
/// stops; the voice slice cuts the sound itself. Each turn is shown in the conversation window while it runs and
/// stored when it ends, with the provider, the model, the tokens and the time taken (ADR 0007, section 7).
/// </para>
/// <para>
/// The enabled tools of the registry are given to the model. When it asks for one, the call goes through the
/// decision point of the permissions, runs, and its result goes back as data; the model then continues
/// (ADR 0017, section 4). No call runs automatically: this slice keeps the loop (ADR 0007, section 2).
/// </para>
/// <para>
/// The memory (ADR 0019) is another slice. This one asks it, through <see cref="IConversationMemory"/>, what to
/// recall to the model and after which silence an exchange is old, and tells it by messages which exchanges left
/// the history (<see cref="ExchangesLeftHistory"/>) and what the user said in a stored turn (<see cref="TurnRecorded"/>).
/// </para>
/// </summary>
public sealed partial class ConversationEngine :
    IEventHandler<TurnStarted>,
    IEventHandler<UtteranceEnded>,
    IEventHandler<UtteranceTranscribed>,
    IEventHandler<FirstSentenceReady>,
    IEventHandler<SpeechEnded>,
    IEventHandler<AnswerFinishedWithoutSpeech>,
    IEventHandler<TurnCancelled>,
    IEventHandler<TurnFailed>,
    IDisposable
{
    /// <summary>Most past turns sent to the model, however short they are.</summary>
    public const int MostHistoryTurns = 20;

    /// <summary>Most exchanges announced as out of the history at the start of a turn.</summary>
    public const int MostExchangesAnnounced = 40;

    private readonly IAssistantModel model;
    private readonly SystemPrompt prompt;
    private readonly ConversationJournal journal;
    private readonly IAssistantLanguageReader language;
    private readonly IToolRegistry tools;
    private readonly IPermissionDecision permissions;
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly ILogger<ConversationEngine> logger;
    private readonly IConversationMemory? memory;
    private readonly Lock gate = new();
    private Answering? current;

    /// <summary>The spoken turn whose utterance ended last, and when, for the delay to the first sound.</summary>
    private (TurnId Turn, long At) lastUtteranceEnd;

    /// <summary>Creates the engine.</summary>
    /// <param name="model">The default model (work item 11).</param>
    /// <param name="prompt">The system message.</param>
    /// <param name="journal">The conversation shown and stored.</param>
    /// <param name="language">The language of a typed request.</param>
    /// <param name="tools">The registry of the tools; the enabled ones are given to the model.</param>
    /// <param name="permissions">The decision point every tool call goes through.</param>
    /// <param name="bus">The bus, for the sentences and the messages of the state machine.</param>
    /// <param name="time">Clock of the durations.</param>
    /// <param name="logger">Logs the timings and the failures, never the text (ADR 0004, section 6).</param>
    /// <param name="memory">What to recall to the model, when a memory slice is there.</param>
    public ConversationEngine(
        IAssistantModel model,
        SystemPrompt prompt,
        ConversationJournal journal,
        IAssistantLanguageReader language,
        IToolRegistry tools,
        IPermissionDecision permissions,
        IMessageBus bus,
        TimeProvider time,
        ILogger<ConversationEngine> logger,
        IConversationMemory? memory = null)
    {
        this.memory = memory;
        this.model = model;
        this.prompt = prompt;
        this.journal = journal;
        this.language = language;
        this.tools = tools;
        this.permissions = permissions;
        this.bus = bus;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>The generation of the last turn answered, kept after it ends; for the tests.</summary>
    internal Task LastAnswer { get; private set; } = Task.CompletedTask;

    /// <summary>The storing of the last turn ended; for the tests.</summary>
    internal Task LastSave { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnStarted message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Input == TurnInput.Text && !string.IsNullOrWhiteSpace(message.Text))
        {
            Start(message.Turn, TurnInput.Text, message.Text.Trim(), language.Current);
        }

        // A spoken turn waits for its transcription.
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(UtteranceEnded message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (gate)
        {
            lastUtteranceEnd = (message.Turn, time.GetTimestamp());
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(UtteranceTranscribed message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            // Whisper heard no words: the turn ends without an answer (work item 10 leaves it to this slice).
            LogNothingToAnswer(message.Turn);
            await bus.PublishAsync<AssistantInput>(new AnswerFinishedWithoutSpeech(message.Turn), CancellationToken.None).ConfigureAwait(false);
            return;
        }

        Start(message.Turn, TurnInput.Voice, message.Text.Trim(), message.Language);
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(FirstSentenceReady message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Answering? answering;
        long endedAt;
        lock (gate)
        {
            answering = current?.Turn == message.Turn ? current : null;
            endedAt = lastUtteranceEnd.Turn == message.Turn ? lastUtteranceEnd.At : 0;
        }

        if (answering is not null && endedAt != 0)
        {
            var delay = time.GetElapsedTime(endedAt);
            LogFirstSound(message.Turn, (long)delay.TotalMilliseconds);
            journal.Show(answering.Update(turn => turn with { FirstSound = delay }));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(SpeechEnded message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        End(message.Turn, TurnOutcome.Answered, null);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(AnswerFinishedWithoutSpeech message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        End(message.Turn, TurnOutcome.Answered, null);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnCancelled message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        End(message.Turn, TurnOutcome.Interrupted, null);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnFailed message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The model failure set by this slice names the cause more precisely than the fault (KeyRejected…).
        End(message.Turn, TurnOutcome.Failed, message.Fault.Cause.ToString());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Answering? stopped;
        lock (gate)
        {
            stopped = current;
            current = null;
        }

        stopped?.Cancel.Cancel();
    }

    /// <summary>The past turns that fit in about half of the context of the model (ADR 0007, section 6).</summary>
    /// <param name="turns">The turns of the journal, oldest first.</param>
    /// <param name="contextLength">The context of the model, in tokens.</param>
    /// <returns>The messages, oldest first: a request then its answer.</returns>
    internal static IReadOnlyList<ChatMessage> History(IReadOnlyList<ConversationTurn> turns, int contextLength) =>
        Messages(Recent(turns, contextLength, DateTimeOffset.MaxValue, null));

    /// <summary>The messages of <paramref name="turns"/>: a request then its answer.</summary>
    internal static IReadOnlyList<ChatMessage> Messages(IReadOnlyList<ConversationTurn> turns) =>
        [.. turns.SelectMany(turn => new[]
        {
            new ChatMessage(ChatRole.User, turn.Request),
            new ChatMessage(ChatRole.Assistant, turn.Answer),
        })];

    /// <summary>
    /// The answered turns sent to the model word for word: the most recent ones that fit in about half of its
    /// context, <see cref="MostHistoryTurns"/> at most and, when age counts, none from before a silence of
    /// <paramref name="oldAfter"/> (ADR 0019, section 5).
    /// </summary>
    /// <param name="turns">The turns of the journal, oldest first.</param>
    /// <param name="contextLength">The context of the model, in tokens.</param>
    /// <param name="now">The moment of the new request.</param>
    /// <param name="oldAfter">The silence after which what came before is old, or <see langword="null"/>.</param>
    /// <returns>The turns kept, oldest first.</returns>
    internal static IReadOnlyList<ConversationTurn> Recent(IReadOnlyList<ConversationTurn> turns, int contextLength, DateTimeOffset now, TimeSpan? oldAfter)
    {
        // About 3 characters per token in French; the other half is for the system message, the request and the answer.
        var budget = Math.Max(0, contextLength) * 3 / 2;
        var kept = new List<ConversationTurn>();
        var next = now;
        foreach (var turn in turns.Reverse())
        {
            // A silence is measured between two requests, answered or not: a failed turn is still an activity.
            if (oldAfter is { } silence && next - turn.At >= silence)
            {
                break;
            }

            next = turn.At;
            if (turn.Outcome != TurnOutcome.Answered || string.IsNullOrWhiteSpace(turn.Answer))
            {
                continue;
            }

            var size = turn.Request.Length + turn.Answer.Length;
            if (size > budget || kept.Count == MostHistoryTurns)
            {
                break;
            }

            budget -= size;
            kept.Add(turn);
        }

        kept.Reverse();
        return kept;
    }

    /// <summary>The answered exchanges of <paramref name="turns"/> older than the ones <paramref name="kept"/>, the most recent <see cref="MostExchangesAnnounced"/>.</summary>
    internal static IReadOnlyList<PastExchange> Left(IReadOnlyList<ConversationTurn> turns, IReadOnlyList<ConversationTurn> kept)
    {
        var keys = kept.Select(turn => turn.Key).ToHashSet();
        var from = kept.Count > 0 ? kept[0].At : DateTimeOffset.MaxValue;
        var left = turns
            .Where(turn => turn.Outcome == TurnOutcome.Answered && !string.IsNullOrWhiteSpace(turn.Answer) && !keys.Contains(turn.Key) && turn.At <= from)
            .Select(turn => new PastExchange(turn.Key, turn.At, turn.Request, turn.Answer))
            .ToList();
        return left.Count > MostExchangesAnnounced ? left.GetRange(left.Count - MostExchangesAnnounced, MostExchangesAnnounced) : left;
    }

    private void Start(TurnId turn, TurnInput input, string request, AssistantLanguage turnLanguage)
    {
        var answering = new Answering(turn, input, new ConversationTurn
        {
            At = time.GetUtcNow(),
            Input = input,
            Request = request,
            Language = turnLanguage.Code,
            Outcome = TurnOutcome.Pending,
        });

        Answering? replaced;
        lock (gate)
        {
            // The state machine cancels a turn before it starts another; this is only a guard.
            replaced = current;
            current = answering;
            answering.Work = Task.Run(() => AnswerAsync(answering, turnLanguage));
            LastAnswer = answering.Work;
        }

        if (replaced is not null)
        {
            End(replaced, TurnOutcome.Interrupted, null);
        }
    }

    private async Task AnswerAsync(Answering answering, AssistantLanguage turnLanguage)
    {
        var token = answering.Cancel.Token;
        var speak = answering.Input == TurnInput.Voice;
        var started = time.GetTimestamp();
        try
        {
            // The history is read before the new turn is shown, so the request is not sent twice.
            await journal.EnsureLoadedAsync(token).ConfigureAwait(false);
            var history = journal.Turns;
            journal.Show(answering.Update(turn => turn));

            // When Ollama freed the model, this announces the loading (the window says so) and waits for it.
            await model.EnsureLoadedAsync(token).ConfigureAwait(false);
            var client = model.Client ?? throw new LlmException(LlmFailure.NotConfigured, "No default provider is configured.");
            var provider = model.Provider;
            journal.Show(answering.Update(turn => turn with { Provider = provider?.Name, Model = provider?.Model }));

            var contextLength = provider?.Tuning.ContextLength ?? new LlmTuning().ContextLength;
            var recall = await RecallAsync(answering.Turn, turnLanguage, contextLength, token).ConfigureAwait(false);
            var recent = Recent(history, contextLength, answering.Snapshot.At, recall.OldAfter);
            var messages = Messages(recent).ToList();
            messages.Add(new ChatMessage(ChatRole.User, answering.Snapshot.Request));

            // Only the enabled tools are offered (ADR 0017, section 4), described in the language of the turn.
            var offered = Declarations(turnLanguage);
            var requester = new ActionRequester(RequesterKind.Tool, provider?.Id ?? "model");
            var denied = new HashSet<string>(StringComparer.Ordinal);
            var callsMade = 0;
            var outside = new OutsideContent();

            // What left the history is told to whoever keeps a summary of it; never a reason to hold the answer.
            if (memory is not null && Left(history, recent) is { Count: > 0 } left)
            {
                await bus.PublishAsync(new ExchangesLeftHistory(left, turnLanguage), CancellationToken.None).ConfigureAwait(false);
            }

            var splitter = new SentenceSplitter();
            var answer = new StringBuilder();
            string? answeredBy = null;
            long? inputTokens = null;
            long? outputTokens = null;
            started = time.GetTimestamp();
            for (var round = 0; ; round++)
            {
                // At the bound of the calls the model gets no tool any more: it answers with what it has.
                var withTools = offered.Count > 0 && callsMade < MostToolCalls && round <= MostToolCalls;
                var options = new ChatOptions
                {
                    Instructions = prompt.Build(turnLanguage, offered.Count > 0, recall.Block),
                    Tools = withTools ? offered : null,
                };
                var calls = new List<FunctionCallContent>();
                var roundText = new StringBuilder();
                await foreach (var update in client.GetStreamingResponseAsync(messages, options, token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    answeredBy = update.ModelId ?? answeredBy;
                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case UsageContent usage:
                                // Each round is a request: the tokens of the turn are their sum.
                                inputTokens = Sum(inputTokens, usage.Details.InputTokenCount);
                                outputTokens = Sum(outputTokens, usage.Details.OutputTokenCount);
                                break;
                            case FunctionCallContent call:
                                calls.Add(call);
                                break;
                        }
                    }

                    var text = update.Text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (roundText.Length == 0 && answer.Length > 0 && !char.IsWhiteSpace(answer[^1]))
                    {
                        // What the model wrote before a tool call and what it writes after are two sentences.
                        answer.Append(' ');
                    }

                    roundText.Append(text);
                    answer.Append(text);
                    var written = answer.ToString();
                    journal.Show(answering.Update(turn => turn with { Answer = written }));
                    if (speak)
                    {
                        await SayAsync(answering.Turn, splitter.Push(text)).ConfigureAwait(false);
                    }
                }

                if (calls.Count == 0 || !withTools)
                {
                    break;
                }

                if (speak)
                {
                    // What was written before the calls is said now, not after their results.
                    await SayAsync(answering.Turn, splitter.Flush()).ConfigureAwait(false);
                }

                // The calls run one by one, in the order asked, each through the decision point.
                messages.Add(new ChatMessage(ChatRole.Assistant, [.. Preface(roundText), .. calls]));
                foreach (var call in calls)
                {
                    var result = await CallAsync(answering, call, requester, turnLanguage, denied, outside, callsMade++, token).ConfigureAwait(false);
                    messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]));
                }
            }

            if (speak)
            {
                await SayAsync(answering.Turn, splitter.Flush()).ConfigureAwait(false);
            }

            var elapsed = time.GetElapsedTime(started);
            var finished = answering.Update(turn => turn with
            {
                Answer = answer.ToString().Trim(),
                Model = answeredBy ?? turn.Model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Duration = elapsed,
            });
            journal.Show(finished);
            LogAnswered(answering.Turn, finished.Provider, finished.Model, (long)elapsed.TotalMilliseconds, inputTokens, outputTokens);

            token.ThrowIfCancellationRequested();
            await bus.PublishAsync<IEvent>(
                speak ? new AnswerSentencesEnded(answering.Turn) : new AnswerFinishedWithoutSpeech(answering.Turn),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Interrupted (US1.12): TurnCancelled already ended the turn.
            MarkCallsInterrupted(answering);
        }
        catch (LlmException exception)
        {
            LogModelFailed(exception, answering.Turn, exception.Failure);
            answering.Update(turn => turn with { Failure = exception.Failure.ToString(), Duration = time.GetElapsedTime(started) });
            var cause = exception.Failure == LlmFailure.ModelMissing ? FaultCause.ModelMissing : FaultCause.ModelUnreachable;
            await bus.PublishAsync<AssistantInput>(
                new TurnFailed(answering.Turn, new AssistantFault(cause, exception.Failure.ToString(), FaultOrigin.LanguageModel)),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogAnswerFailed(exception, answering.Turn);
            answering.Update(turn => turn with { Failure = exception.GetType().Name });
            await bus.PublishAsync<AssistantInput>(
                new TurnFailed(answering.Turn, new AssistantFault(FaultCause.ModelUnreachable, exception.GetType().Name, FaultOrigin.LanguageModel)),
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>What the memory recalls; a memory that fails never holds the answer.</summary>
    private async ValueTask<MemoryRecall> RecallAsync(TurnId turn, AssistantLanguage turnLanguage, int contextLength, CancellationToken token)
    {
        if (memory is null)
        {
            return MemoryRecall.Nothing;
        }

        try
        {
            return await memory.RecallAsync(turnLanguage, contextLength, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRecallFailed(exception, turn);
            return MemoryRecall.Nothing;
        }
    }

    private async Task SayAsync(TurnId turn, IReadOnlyList<string> sentences)
    {
        foreach (var sentence in sentences)
        {
            await bus.PublishAsync(new SentenceToSpeak(turn, sentence), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void End(TurnId turn, TurnOutcome outcome, string? failure)
    {
        Answering? ended;
        lock (gate)
        {
            ended = current?.Turn == turn ? current : null;
        }

        if (ended is not null)
        {
            End(ended, outcome, failure);
        }
    }

    /// <summary>Ends <paramref name="answering"/> once: stops its generation, then stores it when the generation has stopped.</summary>
    private void End(Answering answering, TurnOutcome outcome, string? failure)
    {
        lock (gate)
        {
            if (answering.Ended)
            {
                return;
            }

            answering.Ended = true;
            if (current == answering)
            {
                current = null;
            }
        }

        answering.Cancel.Cancel();
        LastSave = StoreAsync(answering, outcome, failure);
    }

    private async Task StoreAsync(Answering answering, TurnOutcome outcome, string? failure)
    {
        try
        {
            // Never awaited inside a bus handler: the generation may be publishing on the bus.
            await (answering.Work ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogAnswerFailed(exception, answering.Turn);
        }

        var stored = answering.Update(turn => turn with
        {
            Outcome = outcome,
            Answer = turn.Answer.Trim(),
            Failure = outcome == TurnOutcome.Failed ? turn.Failure ?? failure : null,
        });
        await journal.SaveAsync(stored).ConfigureAwait(false);
        answering.Cancel.Dispose();
        if (AssistantLanguage.TryParse(stored.Language, out var spoken) && stored.Request.Length > 0)
        {
            // Only the words of the user leave this slice (ADR 0019, section 3).
            await bus.PublishAsync(new TurnRecorded(answering.Turn, stored.Key, stored.At, stored.Request, spoken), CancellationToken.None).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: Whisper heard no words; nothing to answer.")]
    private partial void LogNothingToAnswer(TurnId turn);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: first sound {Milliseconds} ms after the end of the utterance.")]
    private partial void LogFirstSound(TurnId turn, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: answered by {Provider} ({Model}) in {Milliseconds} ms, {InputTokens} tokens in, {OutputTokens} out.")]
    private partial void LogAnswered(TurnId turn, string? provider, string? model, long milliseconds, long? inputTokens, long? outputTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Turn}: the model did not answer ({Failure}).")]
    private partial void LogModelFailed(Exception exception, TurnId turn, LlmFailure failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Turn}: the memory could not be read; the turn goes on without it.")]
    private partial void LogRecallFailed(Exception exception, TurnId turn);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Turn}: the answer failed.")]
    private partial void LogAnswerFailed(Exception exception, TurnId turn);

    /// <summary>Whether the turn already holds the result of a tool that reads a content from outside.</summary>
    private sealed class OutsideContent
    {
        public bool Read { get; set; }
    }

    /// <summary>A turn being answered: its entry in the journal, its generation and its cancellation.</summary>
    private sealed class Answering(TurnId turn, TurnInput input, ConversationTurn entry)
    {
        private readonly Lock entryGate = new();
        private ConversationTurn entry = entry;

        public TurnId Turn { get; } = turn;

        public TurnInput Input { get; } = input;

        public CancellationTokenSource Cancel { get; } = new();

        public Task? Work { get; set; }

        /// <summary>Guarded by the gate of the engine.</summary>
        public bool Ended { get; set; }

        public ConversationTurn Snapshot
        {
            get
            {
                lock (entryGate)
                {
                    return entry;
                }
            }
        }

        public ConversationTurn Update(Func<ConversationTurn, ConversationTurn> change)
        {
            lock (entryGate)
            {
                entry = change(entry);
                return entry;
            }
        }
    }
}
