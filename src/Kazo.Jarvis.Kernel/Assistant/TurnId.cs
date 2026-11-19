namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// Identifier of a conversation turn (ADR 0005, section 3). Every message of a turn carries it, so a
/// message from a cancelled turn is recognized and ignored. Numbers follow each other, which keeps the
/// state machine a pure, deterministic function.
/// </summary>
/// <param name="Value">Sequence number of the turn since start-up, from 1.</param>
public readonly record struct TurnId(long Value)
{
    /// <summary>Before the first turn.</summary>
    public static TurnId None => default;

    /// <summary>The identifier of the turn after this one.</summary>
    /// <returns>A new identifier.</returns>
    public TurnId Next() => new(Value + 1);

    /// <inheritdoc />
    public override string ToString() => $"turn {Value}";
}
