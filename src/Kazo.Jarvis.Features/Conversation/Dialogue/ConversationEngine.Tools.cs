using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Permissions;
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

    /// <summary>The enabled tools as the model receives them: name, description in the language of the turn, schema.</summary>
    private List<AITool> Declarations(AssistantLanguage turnLanguage) =>
        [.. tools.Enabled.Select(tool => (AITool)AIFunctionFactory.CreateDeclaration(tool.Name, tool.Describe(turnLanguage), tool.Schema))];

    /// <summary>The text the model wrote before its calls, kept in the message that carries them.</summary>
    private static IEnumerable<AIContent> Preface(StringBuilder text) =>
        text.Length == 0 ? [] : [new TextContent(text.ToString())];

    private static long? Sum(long? total, long? more) => more is null ? total : (total ?? 0) + more;

    /// <summary>
    /// One call: the decision point says whether it may run, the tool runs within the time limit, and what goes back
    /// to the model is data in every case: the result, or one sentence naming the cause of the denial or failure.
    /// </summary>
    private async Task<string> CallAsync(
        Answering answering,
        FunctionCallContent call,
        ActionRequester requester,
        AssistantLanguage turnLanguage,
        HashSet<string> denied,
        int index,
        CancellationToken token)
    {
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

        using var limit = new CancellationTokenSource(CallTimeout, time);
        using var both = CancellationTokenSource.CreateLinkedTokenSource(token, limit.Token);
        try
        {
            // WaitAsync: a tool that ignores its cancellation does not hold the turn.
            var result = await tool.InvokeAsync(arguments.Value, both.Token).AsTask().WaitAsync(both.Token).ConfigureAwait(false) ?? string.Empty;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Turn}: the tool {Tool} failed.")]
    private partial void LogToolFailed(Exception exception, TurnId turn, string tool);
}
