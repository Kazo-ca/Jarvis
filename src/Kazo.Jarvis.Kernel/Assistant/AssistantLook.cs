namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// The single look the orb shows (ADR 0005, section 2, as completed by ADR 0011, section 4). The drawing of
/// each look belongs to the orb slice.
/// </summary>
public enum AssistantLook
{
    /// <summary>Still, low brightness, cyan.</summary>
    Idle,

    /// <summary>Gentle rotation, cyan.</summary>
    Listening,

    /// <summary>Fast rotation, violet.</summary>
    Thinking,

    /// <summary>The light follows the voice, cyan.</summary>
    Speaking,

    /// <summary>Slow rotation and progress ring, green: idle with at least one delegated task.</summary>
    LongTask,

    /// <summary>A flash on entry, then still, red.</summary>
    Error,

    /// <summary>Still, gray, labelled "microphone muted".</summary>
    MicrophoneMuted,

    /// <summary>Slow pulse, amber: a confirmation request waits for the user (ADR 0023, section 1).</summary>
    Confirmation,
}
