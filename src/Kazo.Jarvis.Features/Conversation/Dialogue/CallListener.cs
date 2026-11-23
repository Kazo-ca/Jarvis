using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// During a call, Jarvis listens to what follows without « Hey Jarvis » (ADR 0025, sections 12 and 13, point 3):
/// when a spoken turn ends and Jarvis is at rest, listening starts again. The mute of the microphone and the orb
/// keep their rules (ADR 0005): the activation is an ordinary one, refused while the microphone is muted. A turn
/// the user cancelled, or a typed one, does not start listening: the user then speaks when they want, with the
/// wake word or a click.
/// </summary>
public sealed class CallListener : IEventHandler<TurnStarted>, IEventHandler<TurnCancelled>, IEventHandler<AssistantStateChanged>
{
    private readonly CallMode call;
    private readonly IMessageBus bus;
    private readonly Lock gate = new();
    private TurnId spoken = TurnId.None;

    /// <summary>Creates the listener.</summary>
    /// <param name="call">The call.</param>
    /// <param name="bus">The bus the activation goes to.</param>
    public CallListener(CallMode call, IMessageBus bus)
    {
        this.call = call;
        this.bus = bus;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnStarted message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (gate)
        {
            // An announcement or a typed request says nothing of whether the user is still talking.
            spoken = message.Input == TurnInput.Voice ? message.Turn : TurnId.None;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(TurnCancelled message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Nothing heard: Jarvis listens again, a few times. Any other cancellation is the user's (a click, the
        // mute, an interruption): Jarvis does not insist. The state is already at rest when this arrives.
        if (message.Reason != TurnCancelReason.NothingHeard || !IsSpoken(message.Turn) || !call.Active)
        {
            return ValueTask.CompletedTask;
        }

        call.Heard(false);
        return ListenAsync();
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(AssistantStateChanged message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Previous.Phase == AssistantPhase.Listening && message.Current.Phase == AssistantPhase.Thinking)
        {
            call.Heard(true);
            return ValueTask.CompletedTask;
        }

        // A spoken turn that was answered: from thinking or speaking to rest, without a fault.
        var answered = message.Previous.Phase is AssistantPhase.Thinking or AssistantPhase.Speaking
            && message.Current.Phase == AssistantPhase.Idle
            && message.Current.Fault is null
            && !message.Current.MicrophoneMuted
            && IsSpoken(message.Previous.CurrentTurn);
        return answered ? ListenAsync() : ValueTask.CompletedTask;
    }

    private bool IsSpoken(TurnId turn)
    {
        lock (gate)
        {
            return spoken != TurnId.None && turn == spoken;
        }
    }

    private async ValueTask ListenAsync()
    {
        if (call.Listens)
        {
            await bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.Call), CancellationToken.None).ConfigureAwait(false);
        }
    }
}
