namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>What happened when the settings file was loaded (ADR 0004, section 2).</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="FileVersion">The <c>schemaVersion</c> read in the file, when there was one.</param>
public sealed record SettingsLoadState(SettingsLoadOutcome Outcome, int? FileVersion = null)
{
    /// <summary>
    /// Whether the settings may be saved. Never for a file that cannot be read, that a newer version wrote or
    /// whose migration failed: it is kept as it is and the application runs on default values.
    /// </summary>
    public bool CanSave => Outcome is SettingsLoadOutcome.Loaded or SettingsLoadOutcome.Missing or SettingsLoadOutcome.Migrated;

    /// <summary>Whether there was no settings file: the first-run wizard starts (US1.4).</summary>
    public bool IsFirstRun => Outcome == SettingsLoadOutcome.Missing;
}

/// <summary>Outcome of loading the settings file.</summary>
public enum SettingsLoadOutcome
{
    /// <summary>The file was read, at the current version.</summary>
    Loaded,

    /// <summary>There was no file. The first save creates it.</summary>
    Missing,

    /// <summary>An older file was copied to <c>backups\</c>, migrated and written back.</summary>
    Migrated,

    /// <summary>The file is not valid JSON or has no <c>schemaVersion</c>. A copy went to <c>backups\</c>; the file is left untouched.</summary>
    Unreadable,

    /// <summary>A newer version of the application wrote the file. It is left untouched.</summary>
    NewerVersion,

    /// <summary>A migration failed. The file is left untouched; its copy is in <c>backups\</c>.</summary>
    MigrationFailed,
}
