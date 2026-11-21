using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Permissions.Decision;

/// <summary>
/// The decision point (ADR 0017, section 2). For each action: is the tool known and enabled, what class does its
/// classifier give to this call, then « Lecture » and « Effet courant » are allowed, « Interdite » is denied, and
/// « Sensible » and « Critique » need a human answer. Denied by default at every step, and every decision is logged.
/// <para>
/// The human answer comes from <see cref="IConfirmation"/> (ADR 0023). A host without it denies every call that
/// needs one, since nobody can be asked.
/// </para>
/// </summary>
public sealed partial class PermissionDecisionPoint : IPermissionDecision
{
    /// <summary>The collection of the log.</summary>
    public const string CollectionName = "Permissions.Decisions";

    /// <summary>The longest arguments kept in the log.</summary>
    public const int MostArgumentCharacters = 500;

    private readonly IToolRegistry tools;
    private readonly IDocumentStore store;
    private readonly TimeProvider time;
    private readonly ILogger<PermissionDecisionPoint> logger;
    private readonly IAssistantLanguageReader? language;
    private readonly IConfirmation? confirmation;

    /// <summary>Creates the decision point.</summary>
    /// <param name="tools">The registry of the tools.</param>
    /// <param name="store">The store of the log.</param>
    /// <param name="time">The clock of the log.</param>
    /// <param name="logger">Logs the decisions without the arguments (ADR 0004, section 6).</param>
    /// <param name="language">The language of the summary shown and said in a confirmation request.</param>
    /// <param name="confirmation">Asks the user; without it, a call that needs a confirmation is denied.</param>
    public PermissionDecisionPoint(
        IToolRegistry tools,
        IDocumentStore store,
        TimeProvider time,
        ILogger<PermissionDecisionPoint> logger,
        IAssistantLanguageReader? language = null,
        IConfirmation? confirmation = null)
    {
        this.language = language;
        this.confirmation = confirmation;
        this.tools = tools;
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<PermissionDecision> DecideAsync(ActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var at = time.GetUtcNow();
        var decision = cancellationToken.IsCancellationRequested
            ? PermissionDecision.Deny(DenialCause.Interrupted)
            : await ClassifyAndDecideAsync(request, cancellationToken).ConfigureAwait(false);
        await LogAsync(request, decision, at).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return decision;
    }

    /// <summary>The class of a call: the highest of what the tool declares and what its classifier says for these arguments.</summary>
    /// <param name="tool">The tool.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The class; a classifier can only raise the declared one.</returns>
    internal static ActionClass ClassOf(ITool tool, JsonElement arguments)
    {
        var declared = tool.DeclaredClass;
        var classified = tool.Classify(arguments);
        if (!Enum.IsDefined(declared) || !Enum.IsDefined(classified))
        {
            throw new InvalidOperationException($"The tool '{tool.Name}' gave a class that does not exist.");
        }

        return (ActionClass)Math.Max((int)declared, (int)classified);
    }

    private async ValueTask<PermissionDecision> ClassifyAndDecideAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        // 1. Known and enabled?
        var tool = tools.Find(request.Tool);
        if (tool is null)
        {
            return PermissionDecision.Deny(DenialCause.UnknownTool);
        }

        if (!tools.IsEnabled(tool))
        {
            return PermissionDecision.Deny(DenialCause.ToolDisabled);
        }

        if (request.Arguments is not { ValueKind: JsonValueKind.Object } arguments)
        {
            return PermissionDecision.Deny(DenialCause.UnreadableArguments);
        }

        // 2. The class of this call.
        ActionClass actionClass;
        try
        {
            actionClass = ClassOf(tool, arguments);
        }
        catch (Exception exception)
        {
            LogClassifierFailed(exception, request.Tool);
            return PermissionDecision.Deny(DenialCause.ClassifierFailed);
        }

        return actionClass switch
        {
            // 3. No agreement asked.
            ActionClass.Read or ActionClass.EverydayEffect => PermissionDecision.Allow(actionClass),

            // 4. A human answer decides.
            ActionClass.Sensitive or ActionClass.Critical => await ConfirmAsync(request, tool, arguments, actionClass, cancellationToken).ConfigureAwait(false),

            // 5. Denied without a question; an unknown class is denied too.
            _ => PermissionDecision.Deny(DenialCause.Forbidden, actionClass),
        };
    }

    /// <summary>Step 4: asks the user, and turns the answer into a decision. Anything but an agreement through an accepted channel is a denial.</summary>
    private async ValueTask<PermissionDecision> ConfirmAsync(ActionRequest request, ITool tool, JsonElement arguments, ActionClass actionClass, CancellationToken cancellationToken)
    {
        if (confirmation is null)
        {
            return PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, actionClass);
        }

        string? summary = null;
        try
        {
            summary = tool.Summarize(arguments, language?.Current ?? AssistantLanguage.English);
        }
        catch (Exception exception)
        {
            // The exact detail is shown in every case: the request goes on with the general sentence.
            LogSummaryFailed(exception, request.Tool);
        }

        ConfirmationOutcome outcome;
        try
        {
            outcome = await confirmation.AskAsync(
                new ConfirmationRequest(request.Requester, tool.Name, summary, arguments.GetRawText(), actionClass, request.Context),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PermissionDecision.Deny(DenialCause.Interrupted, actionClass);
        }
        catch (Exception exception)
        {
            LogConfirmationFailed(exception, request.Tool);
            return PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, actionClass);
        }

        // ADR 0023, section 4: "Critical" is approved by a click only, whatever the confirmation slice answered.
        var accepted = outcome.Channel == ConfirmationChannels.Click
            || (outcome.Channel == ConfirmationChannels.Voice && actionClass == ActionClass.Sensitive);
        return outcome.Answer switch
        {
            ConfirmationAnswer.Approved when accepted => PermissionDecision.Allow(actionClass) with { Channel = outcome.Channel },
            ConfirmationAnswer.Approved or ConfirmationAnswer.Refused => PermissionDecision.Deny(DenialCause.Refused, actionClass) with { Channel = outcome.Channel },
            _ => PermissionDecision.Deny(DenialCause.Interrupted, actionClass),
        };
    }

    private async Task LogAsync(ActionRequest request, PermissionDecision decision, DateTimeOffset at)
    {
        LogDecided(request.Requester.Kind, request.Requester.Name, request.Tool, decision.Class, decision.Allowed, decision.Cause, decision.Channel);
        try
        {
            await store.Collection<DecisionRecord>(CollectionName).AddAsync(
                new DecisionRecord
                {
                    At = at,
                    RequesterKind = request.Requester.Kind,
                    Requester = request.Requester.Name,
                    Tool = Cut(request.Tool, 200),
                    Arguments = Cut(request.Arguments?.GetRawText() ?? string.Empty, MostArgumentCharacters),
                    Class = decision.Class,
                    Allowed = decision.Allowed,
                    Cause = decision.Cause,
                    Channel = decision.Channel,
                },
                at,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The decision stands: it never depended on the log.
            LogWriteFailed(exception);
        }
    }

    private static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission: {Kind} {Requester} asked for {Tool}; class {Class}, allowed {Allowed}, cause {Cause}, channel {Channel}.")]
    private partial void LogDecided(RequesterKind kind, string requester, string tool, ActionClass? @class, bool allowed, DenialCause? cause, string? channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The summary of {Tool} failed; the request shows the general sentence.")]
    private partial void LogSummaryFailed(Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The confirmation request for {Tool} failed; the call is denied.")]
    private partial void LogConfirmationFailed(Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The classifier of {Tool} failed; the call is denied.")]
    private partial void LogClassifierFailed(Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A decision could not be written to the log.")]
    private partial void LogWriteFailed(Exception exception);
}
