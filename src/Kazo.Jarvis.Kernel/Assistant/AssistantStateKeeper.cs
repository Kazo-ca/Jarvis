using System.Collections.Concurrent;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// The single writer of the assistant state (ADR 0005, section 4). Receives every
/// <see cref="AssistantInput"/> from the bus, applies them one at a time in arrival order with
/// <see cref="AssistantStateMachine"/>, then publishes <see cref="AssistantStateChanged"/> and the effects.
/// </summary>
/// <remarks>
/// No background thread: the first caller that finds the queue idle drains it. An input published by a
/// subscriber while the queue is being drained (a slice reacting to <see cref="TurnStarted"/>, say) is only
/// queued, so it is processed after the current one and never re-enters the machine.
/// </remarks>
public sealed partial class AssistantStateKeeper : IAssistantStateReader, IEventHandler<AssistantInput>
{
    private readonly IMessageBus bus;
    private readonly ILogger logger;
    private readonly ConcurrentQueue<AssistantInput> inputs = new();
    private AssistantState current = AssistantState.Initial;
    private int draining;

    /// <summary>Creates the keeper.</summary>
    /// <param name="bus">Where the state changes and effects are published.</param>
    /// <param name="logger">Logs the ignored inputs.</param>
    public AssistantStateKeeper(IMessageBus bus, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);
        this.bus = bus;
        this.logger = logger;
    }

    /// <inheritdoc />
    public AssistantState Current => Volatile.Read(ref current);

    /// <inheritdoc />
    public async ValueTask HandleAsync(AssistantInput message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        inputs.Enqueue(message);

        // Only one caller drains at a time; the others return at once and their input waits its turn.
        while (Interlocked.CompareExchange(ref draining, 1, 0) == 0)
        {
            try
            {
                while (inputs.TryDequeue(out var input))
                {
                    await ProcessAsync(input);
                }
            }
            finally
            {
                Volatile.Write(ref draining, 0);
            }

            // An input queued between the last TryDequeue and the release would otherwise wait for the next one.
            if (inputs.IsEmpty)
            {
                break;
            }
        }
    }

    private async ValueTask ProcessAsync(AssistantInput input)
    {
        var previous = Current;
        var transition = AssistantStateMachine.Apply(previous, input);
        if (transition.IsIgnored)
        {
            LogIgnored(input.GetType().Name, previous.Phase, transition.IgnoredBecause!);
            return;
        }

        Volatile.Write(ref current, transition.State);

        // The state change and its effects are part of the transition: they are not cancelled with the
        // caller's token, or a slice would miss a TurnCancelled.
        if (transition.State != previous)
        {
            await bus.PublishAsync(new AssistantStateChanged(previous, transition.State), CancellationToken.None);
        }

        foreach (var effect in transition.Effects)
        {
            await bus.PublishAsync(effect, CancellationToken.None);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Input} ignored while {Phase}: {Reason}.")]
    private partial void LogIgnored(string input, AssistantPhase phase, string reason);
}
