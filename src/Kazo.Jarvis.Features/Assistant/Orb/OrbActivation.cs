using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// What a click on the orb does (US1.6): it publishes an activation, and the state machine decides
/// (start listening, cancel it, or interrupt Jarvis). No double click and no delay yet: the task fan
/// arrives at stage 2 (ADR 0010, section 3).
/// </summary>
/// <remarks>
/// The orb component calls it, and so does the Windows host, whose window catches the press itself to tell a
/// click from a move (ADR 0003, section 2).
/// </remarks>
public sealed class OrbActivation
{
    private readonly IMessageBus bus;

    /// <summary>Creates the activation.</summary>
    /// <param name="bus">Where the activation is published.</param>
    public OrbActivation(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        this.bus = bus;
    }

    /// <summary>Publishes <see cref="ActivationRequested"/> from a click.</summary>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes once the state machine received it.</returns>
    public ValueTask ClickAsync(CancellationToken cancellationToken = default) =>
        bus.PublishAsync(new ActivationRequested(ActivationSource.Click), cancellationToken);
}
