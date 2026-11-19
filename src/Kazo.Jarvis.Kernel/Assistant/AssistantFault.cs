namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>The "fault" condition (ADR 0005, section 1).</summary>
/// <param name="Cause">What is unusable; the slices map it to a text and a corrective action.</param>
/// <param name="Detail">Optional technical detail for the log and the hover text. Never a secret.</param>
public sealed record AssistantFault(FaultCause Cause, string? Detail = null);
