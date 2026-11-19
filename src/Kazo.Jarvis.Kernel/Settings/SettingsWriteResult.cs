namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>Result of <see cref="ISettingsStore.WriteAsync{T}"/>.</summary>
/// <param name="Outcome">Saved, refused by the validation, or refused because the settings cannot be saved.</param>
/// <param name="Problems">The validation problems, empty otherwise.</param>
public sealed record SettingsWriteResult(SettingsWriteOutcome Outcome, IReadOnlyList<string> Problems)
{
    /// <summary>The value was saved.</summary>
    public static SettingsWriteResult Saved { get; } = new(SettingsWriteOutcome.Saved, []);

    /// <summary>Nothing may be saved (see <see cref="SettingsLoadState.CanSave"/>).</summary>
    public static SettingsWriteResult ReadOnly { get; } = new(SettingsWriteOutcome.ReadOnly, []);

    /// <summary>Whether the value was saved.</summary>
    public bool IsSaved => Outcome == SettingsWriteOutcome.Saved;

    /// <summary>The value was not valid.</summary>
    /// <param name="problems">What is wrong with it.</param>
    /// <returns>The result.</returns>
    public static SettingsWriteResult Invalid(IReadOnlyList<string> problems) => new(SettingsWriteOutcome.Invalid, problems);
}

/// <summary>Outcome of a settings save.</summary>
public enum SettingsWriteOutcome
{
    /// <summary>Saved, watchers notified.</summary>
    Saved,

    /// <summary>The value was not valid; nothing was written.</summary>
    Invalid,

    /// <summary>The settings cannot be saved (unreadable or newer file); nothing was written.</summary>
    ReadOnly,
}
