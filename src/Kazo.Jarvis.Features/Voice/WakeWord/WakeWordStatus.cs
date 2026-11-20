namespace Kazo.Jarvis.Features.Voice.WakeWord;

/// <summary>What the wake word does now.</summary>
public enum WakeWordState
{
    /// <summary>Not started, or loading the models.</summary>
    Starting,

    /// <summary>Scores the microphone.</summary>
    Active,

    /// <summary>Silent while Jarvis listens, or while the microphone is muted (US1.5, US1.13).</summary>
    Paused,

    /// <summary>A model file is missing; tried again every <see cref="WakeWordListener.RetryInterval"/>.</summary>
    ModelsMissing,

    /// <summary>The models could not be loaded or run; tried again every <see cref="WakeWordListener.RetryInterval"/>.</summary>
    Failed,
}

/// <summary>What the wake word does now, for the settings page.</summary>
/// <param name="State">The state.</param>
/// <param name="MissingFiles">The missing model files, for <see cref="WakeWordState.ModelsMissing"/>.</param>
public sealed record WakeWordStatus(WakeWordState State, IReadOnlyList<string> MissingFiles)
{
    /// <summary>Before the models are loaded.</summary>
    public static WakeWordStatus Starting { get; } = new(WakeWordState.Starting, []);

    /// <summary>Scoring the microphone.</summary>
    public static WakeWordStatus Active { get; } = new(WakeWordState.Active, []);

    /// <summary>Silent while listening or muted.</summary>
    public static WakeWordStatus Paused { get; } = new(WakeWordState.Paused, []);

    /// <summary>Loading or running failed.</summary>
    public static WakeWordStatus Failed { get; } = new(WakeWordState.Failed, []);
}

/// <summary>The last detection, for the settings page.</summary>
/// <param name="At">When.</param>
/// <param name="Score">The score that passed the threshold.</param>
public sealed record WakeWordDetection(DateTimeOffset At, float Score);
