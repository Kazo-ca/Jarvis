namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>The "fault" condition (ADR 0005, section 1).</summary>
/// <param name="Cause">What is unusable; the slices map it to a text and a corrective action.</param>
/// <param name="Detail">Optional technical detail for the log and the hover text. Never a secret.</param>
/// <param name="Origin">
/// Which part of Jarvis found it, when the publisher knows; <see langword="null"/> lets the faults slice take
/// the usual origin of <paramref name="Cause"/>.
/// </param>
public sealed record AssistantFault(FaultCause Cause, string? Detail = null, FaultOrigin? Origin = null)
{
    /// <summary>
    /// Whether <paramref name="cause"/> from <paramref name="origin"/> designates this fault: same cause, and same
    /// origin when both are known.
    /// </summary>
    /// <param name="cause">The cause that went away.</param>
    /// <param name="origin">Where it went away, or <see langword="null"/> for any origin.</param>
    /// <returns><see langword="true"/> when this fault is the one that went away.</returns>
    public bool Matches(FaultCause cause, FaultOrigin? origin) =>
        Cause == cause && (origin is null || Origin is null || Origin == origin);
}
