using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <content>The tool loop (ADR 0017, section 4): its bounds, and one call from the decision to the result.</content>
public sealed partial class ConversationEngine
{
    /// <summary>Most tool calls in one turn; past it, the model answers with what it has.</summary>
    public const int MostToolCalls = 8;

    /// <summary>Longest result returned to the model; the 4 096-token context holds the tools, the history and the results.</summary>
    public const int MostResultCharacters = 2000;

    /// <summary>Arguments are shown and logged as the model wrote them: « é » and « + » stay readable.</summary>
    private static readonly JsonSerializerOptions ArgumentsJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Longest time one call may take.</summary>
    internal TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Tools as a model receives them: name, description in the language of the turn, schema.</summary>
    private static List<AITool> Declarations(AssistantLanguage turnLanguage, IEnumerable<ITool> offered) =>
        [.. offered.Select(tool => (AITool)AIFunctionFactory.CreateDeclaration(tool.Name, tool.Describe(turnLanguage), tool.Schema))];

    /// <summary>
    /// Who answers first (ADR 0025, sections 4 and 9): the agent the user chose in the conversation window for a
    /// typed request, else the Jarvis agent. <see langword="null"/> without the agents slice.
    /// </summary>
    private AgentDefinition? FirstAgent(Answering answering)
    {
        if (agents is null)
        {
            return null;
        }

        // By voice nobody is named to skip the Jarvis agent (section 9).
        var chosen = answering.Input == TurnInput.Text ? agents.Find(choice?.Chosen) : null;
        return chosen ?? agents.Main;
    }

    /// <summary>
    /// The project a turn starts with (ADR 0025, section 10): the one the user chose in the window for a typed
    /// request, else the one the request names in its words. The Jarvis agent may name another when it hands over.
    /// </summary>
    private ProjectDefinition? FirstProject(Answering answering)
    {
        if (projects is null || agents is null)
        {
            return null;
        }

        var chosen = answering.Input == TurnInput.Text ? projects.Find(choice?.Project) : null;
        return chosen ?? projects.NamedIn(answering.Snapshot.Request);
    }

    /// <summary>
    /// The tools <paramref name="agent"/> sees in this turn: its own, and the hand while it may still change. An
    /// agent the user chose keeps the way back to the Jarvis agent like any other.
    /// </summary>
    private IEnumerable<ITool> ToolsFor(TurnRun run, AgentDefinition? agent)
    {
        if (agents is null || agent is null)
        {
            return tools.Enabled;
        }

        // After an agent gave the turn back, the Jarvis agent answers with what it has: the hand does not go round.
        var offered = agents.ToolsOf(agent);
        return run.Returned
            ? offered.Where(tool => tool.Name != agents.HandToolName && tool.Name != agents.ReturnToolName)
            : offered;
    }

    /// <summary>
    /// A call that changes the hand (ADR 0025, sections 4 and 8): the Jarvis agent names an agent, or an agent says
    /// the request is not for it. The loop of the agent that called stops; the turn continues with the other one.
    /// An unknown name is refused, and the agent that called keeps the hand.
    /// </summary>
    /// <returns>What goes back as the result of the call, or <see langword="null"/> when <paramref name="name"/> is not a tool of the hand.</returns>
    private async Task<(ToolCallOutcome Outcome, string Result)?> ChangeHandAsync(TurnRun run, Speaker speaker, string name, JsonElement arguments)
    {
        if (agents is null || speaker.Agent is not { } current || speaker.Target is not null)
        {
            return null;
        }

        if (name == agents.ReturnToolName)
        {
            run.Returned = true;
            run.Next = agents.Main;
            LogHandReturned(run.Answering.Turn, current.Id);
            return (ToolCallOutcome.Done, SystemPrompt.Text("Agent_Returned", run.Language, agents.Main.Name));
        }

        if (name != agents.HandToolName)
        {
            return null;
        }

        var hand = agents.ReadHand(arguments);
        if (hand.Agent is not { } next || next.Id == current.Id || run.HandedToAgent)
        {
            var known = string.Join(", ", agents.Agents.Where(agent => agent.Id != current.Id && agents.ToolsOf(agent).Any(tool => tool.Name != agents.ReturnToolName)).Select(agent => agent.Name));
            return (ToolCallOutcome.Failed, SystemPrompt.Text("Agent_Unknown", run.Language, hand.Asked ?? string.Empty, known));
        }

        // The Jarvis agent names the project with the agent (section 10); a name no project has changes nothing.
        if (projects?.Find(hand.Project) is { } named)
        {
            run.Project = named;
        }

        run.HandedToAgent = true;
        run.Next = next;
        LogHandChanged(run.Answering.Turn, current.Id, next.Id);
        if (run.Speak && !run.Prefaced)
        {
            // The Jarvis agent says that it hands over (section 9); when the model wrote nothing, a fixed sentence does.
            await SayAsync(run.Answering.Turn, [SystemPrompt.Text("Agent_Announcement", run.Language, next.Name)]).ConfigureAwait(false);
        }

        return (ToolCallOutcome.Done, SystemPrompt.Text("Agent_Handed", run.Language, next.Name));
    }

    /// <summary>The text the model wrote before its calls, kept in the message that carries them.</summary>
    private static IEnumerable<AIContent> Preface(StringBuilder text) =>
        text.Length == 0 ? [] : [new TextContent(text.ToString())];

    private static long? Sum(long? total, long? more) => more is null ? total : (total ?? 0) + more;

    /// <summary>
    /// One call: the decision point says whether it may run, the tool runs within the time limit, and what goes back
    /// to the model is data in every case: the result, or one sentence naming the cause of the denial or failure.
    /// </summary>
    private async Task<string> CallAsync(
        TurnRun run,
        Speaker speaker,
        FunctionCallContent call,
        int index,
        CancellationToken token)
    {
        var answering = run.Answering;
        var turnLanguage = run.Language;
        var denied = run.Denied;
        var outside = run.Outside;
        var requester = speaker.Requester;
        var started = time.GetTimestamp();

        // Arguments the adapter could not read stay absent: the decision point denies them.
        JsonElement? arguments = call.Exception is null
            ? JsonSerializer.SerializeToElement(call.Arguments ?? new Dictionary<string, object?>(), ArgumentsJson)
            : null;
        var argumentsText = arguments?.GetRawText() ?? string.Empty;
        var position = 0;
        journal.Show(answering.Update(turn =>
        {
            position = turn.ToolCalls.Count;
            return turn with { ToolCalls = [.. turn.ToolCalls, new ToolCallEntry { Name = call.Name, Arguments = argumentsText, Outcome = ToolCallOutcome.Running }] };
        }));

        string Finish(ToolCallOutcome outcome, string result, ActionClass? actionClass = null, string? denial = null)
        {
            var elapsed = time.GetElapsedTime(started);
            journal.Show(answering.Update(turn => turn with
            {
                ToolCalls = [.. turn.ToolCalls.Select((entry, at) => at == position
                    ? entry with { Outcome = outcome, Result = result, Class = actionClass?.ToString(), Denial = denial, Duration = elapsed }
                    : entry)],
            }));
            LogToolCall(answering.Turn, call.Name, outcome, actionClass, denial, (long)elapsed.TotalMilliseconds);
            return result;
        }

        if (index >= MostToolCalls)
        {
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_CallLimit", turnLanguage, MostToolCalls), denial: "CallLimit");
        }

        // A denied action cannot be asked again in the same turn (ADR 0017, section 2).
        var signature = call.Name + "\n" + argumentsText;
        if (denied.Contains(signature))
        {
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_Again", turnLanguage), denial: "Again");
        }

        // An agent sees its own tools only (ADR 0025, section 1): it cannot call a tool of another agent. A name no
        // tool has goes on to the decision point, which names that cause.
        if (speaker.Target is null && speaker.Agent is not null && tools.Find(call.Name) is not null && !speaker.Offered.Any(offered => offered.Name == call.Name))
        {
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_NotThisAgent", turnLanguage), denial: "NotThisAgent");
        }

        // A target is given the reading tools only (ADR 0020, section 4): it cannot call what it was not offered.
        if (speaker.Target is not null && !speaker.Offered.Any(offered => offered.Name == call.Name))
        {
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_ToolDisabled", turnLanguage), denial: DenialCause.ToolDisabled.ToString());
        }

        // A tool that writes what the model tells it is refused once the model has read a text from outside
        // (ADR 0019, section 3): the user asks again, in a turn where only they spoke.
        if (outside.Read && tools.Find(call.Name) is { RefusedAfterOutsideContent: true })
        {
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_AfterOutsideContent", turnLanguage), denial: "AfterOutsideContent");
        }

        var decision = await permissions.DecideAsync(new ActionRequest(requester, call.Name, arguments) { Context = new ActionContext(answering.Turn, answering.Input == TurnInput.Voice) },
            token).ConfigureAwait(false);
        var tool = decision.Allowed ? tools.Find(call.Name) : null;
        if (tool is null || arguments is null)
        {
            // Allowed but gone since (a server that disconnected): nothing runs.
            var cause = decision.Cause ?? DenialCause.UnknownTool;
            denied.Add(signature);
            return Finish(ToolCallOutcome.Denied, SystemPrompt.Text("Denied_" + cause, turnLanguage), decision.Class, cause.ToString());
        }

        outside.Read |= tool.ReadsOutsideContent;
        using var limit = new CancellationTokenSource(CallTimeout, time);
        using var both = CancellationTokenSource.CreateLinkedTokenSource(token, limit.Token);
        try
        {
            if (speaker.Target is null && handOff is not null && call.Name == handOff.ToolName)
            {
                // The terminal tool: its run is the whole answer of the target, without the time limit of a call.
                var asked = handOff.Read(arguments.Value) ?? new HandOffCall(HandOffKind.Question, null);
                return Finish(ToolCallOutcome.Done, await HandOffAsync(run, asked, null, token).ConfigureAwait(false), decision.Class);
            }

            if (await ChangeHandAsync(run, speaker, call.Name, arguments.Value).ConfigureAwait(false) is { } hand)
            {
                return Finish(hand.Outcome, hand.Result, decision.Class);
            }

            // WaitAsync: a tool that ignores its cancellation does not hold the turn.
            var result = await tool.InvokeAsync(arguments.Value, speaker.Agent?.Id, both.Token).AsTask().WaitAsync(both.Token).ConfigureAwait(false) ?? string.Empty;
            if (result.Length > MostResultCharacters)
            {
                result = result[..MostResultCharacters] + "\n" + SystemPrompt.Text("Truncated", turnLanguage, MostResultCharacters);
            }

            return Finish(ToolCallOutcome.Done, result, decision.Class);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // An interruption (US1.12): the turn ends here; the entry is marked with the others still running.
            throw;
        }
        catch (OperationCanceledException)
        {
            return Finish(ToolCallOutcome.Failed, SystemPrompt.Text("ToolTimedOut", turnLanguage, (int)CallTimeout.TotalSeconds), decision.Class);
        }
        catch (ToolException exception)
        {
            return Finish(ToolCallOutcome.Failed, SystemPrompt.Text("ToolFailed", turnLanguage, exception.Message), decision.Class);
        }
        catch (Exception exception)
        {
            LogToolFailed(exception, answering.Turn, call.Name);
            return Finish(ToolCallOutcome.Failed, SystemPrompt.Text("ToolFailedUnexpectedly", turnLanguage), decision.Class);
        }
    }

    /// <summary>After an interruption, the calls that had not ended are shown and stored as interrupted.</summary>
    private void MarkCallsInterrupted(Answering answering)
    {
        if (answering.Snapshot.ToolCalls.Any(entry => entry.Outcome == ToolCallOutcome.Running))
        {
            journal.Show(answering.Update(turn => turn with
            {
                ToolCalls = [.. turn.ToolCalls.Select(entry => entry.Outcome == ToolCallOutcome.Running ? entry with { Outcome = ToolCallOutcome.Interrupted } : entry)],
            }));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: tool {Tool} {Outcome} (class {Class}, cause {Denial}) in {Milliseconds} ms.")]
    private partial void LogToolCall(TurnId turn, string tool, ToolCallOutcome outcome, ActionClass? @class, string? denial, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: the agent {From} handed the turn to the agent {To}.")]
    private partial void LogHandChanged(TurnId turn, string from, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Turn}: the agent {From} gave the turn back to the Jarvis agent.")]
    private partial void LogHandReturned(TurnId turn, string from);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Turn}: the tool {Tool} failed.")]
    private partial void LogToolFailed(Exception exception, TurnId turn, string tool);
}
