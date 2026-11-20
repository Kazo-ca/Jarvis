using System.Text.Json.Nodes;

namespace Kazo.Jarvis.Kernel.Settings;

/// <summary>
/// Moves the settings file from one <c>schemaVersion</c> to the next (ADR 0004, section 2). The file is
/// copied to <c>backups\</c> before the first migration runs. Registered by the slice whose section changes.
/// </summary>
public interface ISettingsMigration
{
    /// <summary>The version this migration starts from; it leaves the file at <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    /// <summary>Changes the file content in place. Must keep the sections it does not know.</summary>
    /// <param name="root">The whole file, <c>schemaVersion</c> included (the store updates it).</param>
    void Migrate(JsonObject root);
}

/// <summary>Version of the settings file written by this build.</summary>
public static class SettingsSchema
{
    /// <summary>The current <c>schemaVersion</c>. Raising it needs an <see cref="ISettingsMigration"/> from the previous one.</summary>
    public const int CurrentVersion = 1;
}
