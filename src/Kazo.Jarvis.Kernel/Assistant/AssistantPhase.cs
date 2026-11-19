namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>The exclusive phase of the assistant (ADR 0005, section 1).</summary>
public enum AssistantPhase
{
    /// <summary>Waits for an activation.</summary>
    Idle,

    /// <summary>Captures the request.</summary>
    Listening,

    /// <summary>Transcribes, then waits for the model.</summary>
    Thinking,

    /// <summary>Says the answer.</summary>
    Speaking,
}
