namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>The pure look function, the same for every host (ADR 0005, section 2; ADR 0011, section 4; ADR 0023, section 1).</summary>
public static class AssistantLooks
{
    /// <summary>Computes the look of a state.</summary>
    /// <param name="state">The assistant state.</param>
    /// <returns>
    /// InteractionRequired while something waits for the user (ADR 0023); otherwise the look of the phase when Listening, Thinking or Speaking; otherwise Error if there is a fault;
    /// otherwise MicrophoneMuted if muted; otherwise LongTask if a delegated task runs; otherwise Idle.
    /// </returns>
    public static AssistantLook Of(AssistantState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Phase switch
        {
            _ when state.Interaction is not null => AssistantLook.InteractionRequired,
            AssistantPhase.Listening => AssistantLook.Listening,
            AssistantPhase.Thinking => AssistantLook.Thinking,
            AssistantPhase.Speaking => AssistantLook.Speaking,
            _ when state.Fault is not null => AssistantLook.Error,
            _ when state.MicrophoneMuted => AssistantLook.MicrophoneMuted,
            _ when state.DelegatedTasks > 0 => AssistantLook.LongTask,
            _ => AssistantLook.Idle,
        };
    }

    /// <summary>
    /// Whether the progress ring is drawn over the look: as soon as there is a delegated task, whatever the
    /// look (ADR 0011, section 4).
    /// </summary>
    /// <param name="state">The assistant state.</param>
    /// <returns><see langword="true"/> when at least one delegated task runs.</returns>
    public static bool ShowsProgressRing(AssistantState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.DelegatedTasks > 0;
    }
}
