using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Llm.Providers;

/// <summary>
/// Raises and clears the faults of the language model outside a turn (US1.17, work item stage1/15-faults), from
/// the loads that <see cref="AssistantModelKeeper"/> announces. A missing model is raised at once: it does not come
/// back by itself. An unreachable server is raised only after <see cref="FailuresBeforeFault"/> failed loads in a
/// row, so the orb does not turn red while Docker starts Ollama after a Windows logon. A successful load clears
/// both causes.
/// </summary>
public sealed class LanguageModelFaultWatcher : IEventHandler<AssistantModelLoadChanged>
{
    /// <summary>Failed loads in a row before an unreachable server becomes a fault (the keeper waits 15 s between the first two).</summary>
    public const int FailuresBeforeFault = 2;

    private readonly IMessageBus bus;
    private readonly IAssistantStateReader state;
    private readonly Lock gate = new();
    private int failures;
    private bool loading;

    /// <summary>Creates the watcher.</summary>
    /// <param name="bus">Where the faults are raised and cleared.</param>
    /// <param name="state">Tells whether a fault of the language model is current, so nothing is cleared for nothing.</param>
    public LanguageModelFaultWatcher(IMessageBus bus, IAssistantStateReader state)
    {
        this.bus = bus;
        this.state = state;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(AssistantModelLoadChanged message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        AssistantInput? input;
        lock (gate)
        {
            input = Next(message);
        }

        if (input is not null)
        {
            await bus.PublishAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    private AssistantInput? Next(AssistantModelLoadChanged message)
    {
        switch (message.State)
        {
            case ModelLoadState.Loading:
                loading = true;
                return null;

            case ModelLoadState.Failed:
                loading = false;
                failures++;
                var failure = message.Failure ?? LlmFailure.ServerError;
                if (failure == LlmFailure.ModelMissing)
                {
                    return Raise(FaultCause.ModelMissing, failure);
                }

                return failures >= FailuresBeforeFault ? Raise(FaultCause.ModelUnreachable, failure) : null;

            // Loaded, or Unknown right after a load for a provider that cannot tell (a success).
            case ModelLoadState.Loaded:
            case ModelLoadState.Unknown when loading:
                loading = false;
                failures = 0;
                return state.Current.Fault is { } fault && IsLanguageModelFault(fault)
                    ? new FaultResolved(fault.Cause, FaultOrigin.LanguageModel)
                    : null;

            default:
                loading = false;
                return null;
        }
    }

    // A fault without an origin is the language model's only for an unreachable server (see FaultAdvice.UsualOrigin).
    private static bool IsLanguageModelFault(AssistantFault fault) =>
        fault.Origin == FaultOrigin.LanguageModel && fault.Cause is FaultCause.ModelUnreachable or FaultCause.ModelMissing
        || fault.Origin is null && fault.Cause == FaultCause.ModelUnreachable;

    private static FaultRaised Raise(FaultCause cause, LlmFailure failure) =>
        new(new AssistantFault(cause, failure.ToString(), FaultOrigin.LanguageModel));
}
