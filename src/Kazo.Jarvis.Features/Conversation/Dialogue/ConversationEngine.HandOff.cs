using System.Text;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <content>The hand-off of a turn to a target (ADR 0020, sections 2 to 5), and what a turn shares between the models that answer in it.</content>
public sealed partial class ConversationEngine
{
    /// <summary>
    /// Hands the turn over: asks the hand-off slice who answers, says the fixed sentence that names the target, then
    /// lets the target answer in the loop of the turn. The target gets the personality and the fixed rules, the
    /// same window of history as the local model, the request, and the enabled tools of class « Lecture » without
    /// the hand-off tool; the memory only when the user chose so.
    /// </summary>
    /// <param name="run">The turn.</param>
    /// <param name="asked">The kind and the reason.</param>
    /// <param name="forcedRequest">For a hand-off forced by the first words, what follows the formula; else <see langword="null"/>.</param>
    /// <param name="token">Cancelled by an interruption.</param>
    /// <returns>What the tool call shows once the target answered.</returns>
    /// <exception cref="ToolException">
    /// The hand-off did not happen; the message names the cause in the language of the turn. It goes back to the
    /// local model as data, or is said to the user after a forced hand-off: never a fault of the assistant.
    /// </exception>
    private async Task<string> HandOffAsync(TurnRun run, HandOffCall asked, string? forcedRequest, CancellationToken token)
    {
        var answering = run.Answering;
        var language = run.Language;
        var before = answering.Snapshot;
        var entry = new HandOffEntry { Kind = asked.Kind, Reason = asked.Reason, Forced = forcedRequest is not null, FromProvider = before.Provider, FromModel = before.Model };
        var opening = handOff!.Open(asked.Kind);
        if (opening.Target is not { } target)
        {
            var refusal = (opening.Refusal ?? HandOffRefusal.NoQuestionsTarget).ToString();
            journal.Show(answering.Update(turn => turn with { HandOff = entry with { Failure = refusal } }));
            LogHandOffRefused(answering.Turn, asked.Kind, refusal);
            throw new ToolException(SystemPrompt.Text("HandOff_" + refusal, language));
        }

        entry = entry with { Target = target.Name };
        journal.Show(answering.Update(turn => turn with { HandOff = entry, Provider = target.Provider.Name, Model = target.Provider.Model }));
        if (run.Speak)
        {
            // What the local model said before handing off, then the fixed sentence that names the target.
            await SayAsync(answering.Turn, run.Splitter.Flush()).ConfigureAwait(false);
            await SayAsync(answering.Turn, [SystemPrompt.Text("HandOff_Announcement", language, target.Name)]).ConfigureAwait(false);
        }

        var started = time.GetTimestamp();
        var heard = false;
        void FirstText()
        {
            if (!heard)
            {
                // The delay a real target adds before its first word (measured on Windows, stage 2 plan).
                heard = true;
                var delay = time.GetElapsedTime(started);
                LogHandOffFirstText(answering.Turn, target.Provider.Id, (long)delay.TotalMilliseconds);
                journal.Show(answering.Update(turn => turn with { HandOff = (turn.HandOff ?? entry) with { FirstText = delay } }));
            }
        }

        var messages = Messages(run.Recent).ToList();
        messages.Add(new ChatMessage(ChatRole.User, forcedRequest ?? before.Request));
        var offered = Declarations(language, tools.Enabled.Where(tool => tool.DeclaredClass == ActionClass.Read && tool.Name != handOff.ToolName));
        var speaker = new Speaker(
            target.Client,
            new ActionRequester(RequesterKind.Tool, target.Provider.Id),
            offered,
            target.WithMemory ? run.Recall.Block : null,
            target,
            FirstText,
            () => handOff.Open(asked.Kind).Refusal);
        var written = run.Answer.Length;
        try
        {
            var usage = await RunAsync(run, speaker, messages, token).ConfigureAwait(false);
            if (run.Answer.Length == written)
            {
                throw new LlmException(LlmFailure.ServerError, "The target wrote nothing.");
            }

            run.Handed = usage;
            if (handOff.Notice(language) is { } notice)
            {
                // 80 % of the cap passed, or the cap reached by this answer: said once, after the answer (section 6).
                await WriteAsync(run, notice).ConfigureAwait(false);
            }

            return SystemPrompt.Text("HandOff_Answered", language, target.Name);
        }
        catch (LlmException exception)
        {
            LogHandOffFailed(exception, answering.Turn, target.Provider.Id, exception.Failure);
            journal.Show(answering.Update(turn => turn with
            {
                Provider = before.Provider,
                Model = before.Model,
                HandOff = (turn.HandOff ?? entry) with { Failure = exception.Failure.ToString() },
            }));
            throw new ToolException(SystemPrompt.Text("HandOff_" + exception.Failure, language, target.Name), exception);
        }
        catch (HandOffStoppedException exception)
        {
            // The cap was reached between two requests of the target: it stops there, and the cause says it.
            var refusal = exception.Refusal.ToString();
            _ = handOff.Notice(language);
            LogHandOffRefused(answering.Turn, asked.Kind, refusal);
            journal.Show(answering.Update(turn => turn with
            {
                Provider = before.Provider,
                Model = before.Model,
                HandOff = (turn.HandOff ?? entry) with { Failure = refusal },
            }));
            throw new ToolException(SystemPrompt.Text("HandOff_" + refusal, language), exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: hand-off of kind {Kind} refused ({Refusal}).")]
    private partial void LogHandOffRefused(TurnId turn, HandOffKind kind, string refusal);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: first text of the target {Provider} {Milliseconds} ms after the hand-off.")]
    private partial void LogHandOffFirstText(TurnId turn, string provider, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Turn}: the target {Provider} did not answer ({Failure}).")]
    private partial void LogHandOffFailed(Exception exception, TurnId turn, string provider, LlmFailure failure);

    /// <summary>Whether the turn already holds the result of a tool that reads a content from outside.</summary>
    private sealed class OutsideContent
    {
        public bool Read { get; set; }
    }

    /// <summary>The tokens of one model in a turn, and the name the server gave it.</summary>
    private sealed class Usage
    {
        public long? Input { get; set; }

        public long? Output { get; set; }

        public string? Model { get; set; }
    }

    /// <summary>One model answering in a turn: its client, who it is for the decision point, what it is offered and reminded of.</summary>
    /// <param name="Client">Its client.</param>
    /// <param name="Requester">Who asks, for the decision point and its log.</param>
    /// <param name="Offered">The tools it is given.</param>
    /// <param name="Memory">The block of the memory in its system message, or <see langword="null"/>.</param>
    /// <param name="Target">The target, when this is not the local model.</param>
    /// <param name="FirstText">Called at each piece of text; the hand-off measures its first one.</param>
    /// <param name="Recheck">Asked before each request after the first: why the target must stop, or <see langword="null"/>.</param>
    private sealed record Speaker(
        IChatClient Client,
        ActionRequester Requester,
        List<AITool> Offered,
        string? Memory,
        HandOffTarget? Target = null,
        Action? FirstText = null,
        Func<HandOffRefusal?>? Recheck = null);

    /// <summary>A target stopped between two of its requests.</summary>
    private sealed class HandOffStoppedException(HandOffRefusal refusal) : Exception($"The hand-off stopped: {refusal}.")
    {
        public HandOffRefusal Refusal { get; } = refusal;
    }

    /// <summary>What a turn shares between the models that answer in it: the answer written so far, what is said, what was denied.</summary>
    private sealed class TurnRun(Answering answering, AssistantLanguage language, IReadOnlyList<ConversationTurn> recent, MemoryRecall recall)
    {
        public Answering Answering { get; } = answering;

        public AssistantLanguage Language { get; } = language;

        /// <summary>The turns of the history sent word for word.</summary>
        public IReadOnlyList<ConversationTurn> Recent { get; } = recent;

        public MemoryRecall Recall { get; } = recall;

        public bool Speak { get; } = answering.Input == TurnInput.Voice;

        public SentenceSplitter Splitter { get; } = new();

        public StringBuilder Answer { get; } = new();

        public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);

        public OutsideContent Outside { get; } = new();

        /// <summary>The tokens of the target once it answered; <see langword="null"/> while the local model answers.</summary>
        public Usage? Handed { get; set; }
    }
}
